import asyncio
from prometheus_async.aio.web import start_http_server
from prometheus_client import Gauge
from time import time
from typing import Callable, Awaitable

from mariadb_chat_handler import MariaDbChatHandler
from telegram_handler import TelegramHandler


PROCESS_CHATS_GAUGE = Gauge("process_chats", "Time spent to process all chats")

async def run_in_loop_async(
        to_be_run: Callable[[MariaDbChatHandler, TelegramHandler], Awaitable[None]], 
        args: list, timeframe: float = 60
    ) -> None:
    while True:
        started = time()
        await to_be_run(*args)
        next_execution = started + timeframe
        now = time()
        if now < next_execution:
            await asyncio.sleep(next_execution - now)


async def process_chats(db: MariaDbChatHandler, telegram_handler: TelegramHandler) -> None:
    start = time()
    registered_chats = db.get_registered_chats()
    distinct_gist_ids = set(chat.gist_id_last_sent for chat in registered_chats)
    gists = { gist_id: db.get_next_gists(gist_id) for gist_id in distinct_gist_ids }
    for chat in registered_chats:
        gists = gists[chat.gist_id_last_sent]
        if len(gists) == 0:
            continue
        for gist in gists:
            await telegram_handler.send_gist_async(gist)
        db.set_last_sent_gist(chat.id, gists[-1].id)
    PROCESS_CHATS_GAUGE.set(time() - start)


async def main_async():
    await start_http_server(port=9090)
    db = MariaDbChatHandler()
    telegram_handler = TelegramHandler(db)

    async with telegram_handler:
        await run_in_loop_async(process_chats, [db, telegram_handler])


if __name__ == "__main__":
    asyncio.run(main_async())
