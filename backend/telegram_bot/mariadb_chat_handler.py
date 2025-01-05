from datetime import timezone

from gists_utils.types import Gist
from gists_utils.mariadb_handler import MariaDbHandler, mariadb
from registered_chat_data import ChatInfo


class MariaDbChatHandler(MariaDbHandler):
    def __init__(self):
        super().__init__()

    def get_next_gists(self, last_id: int) -> list[Gist]:
        query = ("SELECT * FROM gists WHERE id > ? AND disabled IS FALSE ORDER BY id ASC")
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (last_id,))
                return [self.query_response_to_gist(response) for response in cur.fetchall()]
        except mariadb.Error as e:
            self._logger.error(f"Error getting newer gists with last_id {last_id}", exc_info=True)
            raise e
        
    def get_registered_chats(self) -> list[ChatInfo]:
        try:
            conn = self._connect()
            with conn.cursor() as cur:
                cur.execute("SELECT id, gist_id_last_sent FROM chats")
                chats = [
                    ChatInfo(id, gist_id_last_sent) 
                    for id, gist_id_last_sent in cur.fetchall()
                ]
            conn.close()
        except mariadb.Error as e:
            self._logger.error("Error getting all registered chats", exc_info=True)
            raise e
        return chats
    
    def chat_is_registered(self, id: int) -> bool:
        try:
            conn = self._connect()
            with conn.cursor() as cur:
                cur.execute("SELECT id FROM chats WHERE id = ?", (id,))
                results = cur.fetchall()
            conn.close()
        except mariadb.Error as e:
            self._logger.error(f"Error checking if chat with id {id} is registered", exc_info=True)
            raise e
        if len(results) > 1:
            self._logger.error(f"Found multiple chats with id {id}")
            raise KeyError(id)
        return len(results) == 1
    
    def _get_most_recent_gist_id(self, cur: mariadb.Cursor) -> int:
        try:
            cur.execute("SELECT id FROM gists WHERE disabled IS FALSE ORDER BY id DESC LIMIT 1")
            result = cur.fetchone()
            return 0 if result is None else result[0]
        except mariadb.Error as e:
            self._logger.error(f"Error getting the most recent gist id", exc_info=True)
            raise e

    def register_chat(self, chat_id: int) -> None:
        try:
            conn = self._connect()
            with conn.cursor() as cur:
                gist_id = max(self._get_most_recent_gist_id(cur) - 5, -1)
                cur.execute("INSERT INTO chats (id, gist_id_last_sent) VALUES (?, ?)", (chat_id, gist_id))
                if cur.rowcount != 1:
                    self._logger.warning(f"Could not register chat with ID {chat_id} and gist ID {gist_id}")
            conn.close()
        except mariadb.Error as e:
            self._logger.error(f"Error registering chat with ID {chat_id} and gist ID {gist_id}", exc_info=True)
            raise e

    def deregister_chat(self, chat_id: int) -> None:
        try:
            conn = self._connect()
            with conn.cursor() as cur:
                cur.execute("DELETE FROM chats WHERE id = ?", (chat_id,))
                if cur.rowcount != 1:
                    self._logger.warning(f"Could not deregister chat with ID {chat_id}")
            conn.close()
        except mariadb.Error as e:
            self._logger.error(f"Error deregistering chat with ID {chat_id}", exc_info=True)
            raise e
    
    def set_last_sent_gist(self, chat_id: int, gist_id: int) -> None:
        try:
            conn = self._connect()
            with conn.cursor() as cur:
                cur.execute(
                    "UPDATE chats SET gist_id_last_sent = ? WHERE id = ?", 
                    (gist_id, chat_id)
                )
                if cur.rowcount != 1:
                    self._logger.warning(
                        f"Could not set last sent gist for chat with ID {chat_id} to {gist_id}"
                    )
            conn.close()
        except mariadb.Error as e:
            self._logger.error(f"Error updating chat with ID {chat_id}", exc_info=True)
            raise e
