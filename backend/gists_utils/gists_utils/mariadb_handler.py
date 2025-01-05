from os import getenv
from datetime import timezone
import mariadb

from gists_utils.types import FeedInfo, Gist, SearchResult
from gists_utils.logger import get_logger


class MariaDbHandler:
    def __init__(self):
        self._host = getenv("DB_HOSTNAME")
        self._db_name = "thegistofitsec"
        self._username = getenv("DB_USERNAME")
        self._password = getenv("DB_PASSWORD")
        self._logger = get_logger("mariadb_handler")
        self._connection = self._connect()

    def _connect(self) -> mariadb.Connection:
        try:
            return mariadb.connect(
                user=self._username,
                password=self._password,
                host=self._host,
                database=self._db_name,
                autocommit=True
            )
        except mariadb.Error as e:
            self._logger.error("Error connecting to mariadb", exc_info=True)
            raise e
    
    @staticmethod
    def query_response_to_gist(response: tuple) -> Gist:
        (
            id, reference, feed_id, author, title, published, 
            updated, link, summary, tags, search_query, disabled
        ) = response
        return Gist(
            id, reference, feed_id, author, title, 
            published.replace(tzinfo=timezone.utc), 
            updated.replace(tzinfo=timezone.utc), 
            link, summary, tags.split(";;"), search_query
        )
        
    def get_gist_by_id(self, gist_id: int) -> Gist | None:
        query = "SELECT * FROM gists WHERE id = ? AND disabled = FALSE"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (gist_id,))
                result = cur.fetchone()
                return None if result is None else self.query_response_to_gist(result)
        except mariadb.Error as e:
            self._logger.error(f"Error getting gist with id '{gist_id}'", exc_info=True)
            raise e
    
    def get_feed_by_id(self, gist_id: int) -> FeedInfo | None:
        query = "SELECT * FROM feeds WHERE id = ?"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (gist_id,))
                results = cur.fetchall()
        except mariadb.Error as e:
            self._logger.error(f"Error getting feed with id {gist_id}", exc_info=True)
            raise e
        if len(results) > 1:
            self._logger.error(f"Found multiple feeds with id {gist_id}")
            raise KeyError(gist_id)
        if len(results) == 0:
            return None
        return FeedInfo(*results[0])
    
    @staticmethod
    def query_response_to_search_result(response: tuple) -> SearchResult:
        result_id, gist_id, title, snippet, link, display_link, thumbnail_link, image_link = response
        return SearchResult(result_id, gist_id, title, snippet, link, display_link, thumbnail_link, image_link)
    
    def get_search_results_by_gist_id(self, gist_id: int) -> list[SearchResult]:
        query = "SELECT * FROM search_results WHERE gist_id = ?"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (gist_id,))
                return [self.query_response_to_search_result(response) for response in cur.fetchall()]
        except mariadb.Error as e:
            self._logger.error(f"Error getting search results for gist with id {gist_id}", exc_info=True)
            raise e
