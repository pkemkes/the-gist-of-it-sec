from os import getenv
from datetime import datetime
from telegram import Update, Bot
from telegram.ext import ApplicationBuilder, CommandHandler, ContextTypes

from gists_utils.types import Gist
from gists_utils.logger import get_logger
from mariadb_chat_handler import MariaDbChatHandler


class TelegramHandler:
    def __init__(self, db: MariaDbChatHandler) -> None:
        self._db = db
        self.application = ApplicationBuilder().token(getenv("TELEGRAM_API_KEY")).build()
        start_handler = CommandHandler("start", self.handle_cmd_start_async)
        self.application.add_handler(start_handler)
        stop_handler = CommandHandler("stop", self.handle_cmd_stop_async)
        self.application.add_handler(stop_handler)
        self._logger = get_logger("telegram_handler")
        self._gist_url = getenv("APP_BASE_URL", "") + "/?gist="
    
    async def __aenter__(self):
        await self.application.initialize()
        await self.application.updater.start_polling()
        await self.application.start()
        self._logger.info("Started polling for incoming telegram commands")

    async def __aexit__(self, exc_t, exc_v, exc_tb):
        await self.application.updater.stop()
        await self.application.stop()
        await self.application.shutdown()
        self._logger.info("Stopped polling for incoming telegram commands")

    async def send_message_async(self, bot: Bot, chat_id: int, message: str, 
                                 parse_mode: str | None = None) -> None:
        await bot.send_message(chat_id, message, parse_mode)
        self._logger.info(f"Sent following message to {chat_id}: {message}")

    async def handle_cmd_start_async(self, update: Update, context: ContextTypes.DEFAULT_TYPE):
        chat_id = update.effective_chat.id
        if self._db.chat_is_registered(chat_id):
            self._logger.warning(f"Could not register chat with ID {chat_id}. It is already registered")
            message = (
                "Seems like you are already registered. "
                "I will continue to send you gists. Happy reading!"
            )
        else:
            self._db.register_chat(chat_id)
            self._logger.info(f"Registered chat with ID {chat_id}")
            message = (
                "Welcome to The Gist of IT Sec! "
                "I registered your chat. "
                "I will regularly send you gists "
                "of the freshest news of selected outlets."
            )
        await self.send_message_async(context.bot, chat_id, message)
    
    async def handle_cmd_stop_async(self, update: Update, context: ContextTypes.DEFAULT_TYPE):
        chat_id = update.effective_chat.id
        if self._db.chat_is_registered(chat_id):
            self._db.deregister_chat(chat_id)
            self._logger.info(f"Deregistered chat with ID {chat_id}")
            message = "Such a shame to see you go. I deregistered you. Goodbye."
        else:
            self._logger.warning(f"Could not deregister chat with ID {chat_id}. It is not registered")
            message = "Seems like you were not registered to begin with. I will not send you gists."
        await self.send_message_async(context.bot, chat_id, message)
        
    @staticmethod
    def escape(message: str) -> str:
        chars = '_*[]()~`>#+-=|{}.!'
        escaped = message
        for char in chars:
            escaped = escaped.replace(char, "\\" + char)
        return escaped
    
    @staticmethod
    def to_timestamp_str(datetime_obj: datetime) -> str:
        return datetime_obj.strftime("%d.%m.%Y %H:%M %Z")

    def build_gist_message(self, gist: Gist) -> str:
        feed = self._db.get_feed_by_id(gist.feed_id)
        date_str = self.to_timestamp_str(gist.published)
        if gist.published != gist.updated:
            date_str += ", updated: " + self.to_timestamp_str(gist.updated)
        return (
            f"*{self.escape(gist.title)}*\n"
            f"{self.escape(date_str)}\n\n"
            f"{self.escape(gist.summary)}\n\n"
            f"Tags: _{self.escape(', '.join(gist.tags))}_\n\n"
            f"{self.escape(feed.title)} \\- "
            f"{self.escape(gist.author)}\n\n"
            f"More details: {self.escape(self._gist_url + str(gist.id))}"
        )

    async def send_gist_async(self, gist: Gist) -> None:
        registered_chats = self._db.get_registered_chats()
        gist = self.build_gist_message(gist)
        for chat in registered_chats:
            await self.send_message_async(self.application.bot, chat.id, gist, "MarkdownV2")
        self._logger.info(f"Sent entry: {gist.title}")
