import mariadb

from gists_utils.types import Gist, FeedInfo
from gists_utils.mariadb_handler import MariaDbHandler


MAXINT = 2**31 - 1


class MariaDbRestHandler(MariaDbHandler):
    def __init__(self):
        super().__init__()
    
    def get_prev_gists(
            self, last_id: int, take: int, search_query: str | None = None, 
            tags: list[str] = [], disabled_feeds: list[int] = [],
        ) -> list[Gist]:
        last_id = last_id if last_id > 0 else MAXINT
        search_words = (
            [f"%{word.strip().lower()}%" for word in search_query.split(" ")] 
            if search_query is not None 
            else []
        )
        search_params = [search_words[i//2] for i in range(len(search_words) * 2)]
        tags = [f"\\b{tag}\\b" for tag in tags]
        params = (last_id, *search_params, *tags, *disabled_feeds, take)

        constraints = ["disabled is FALSE", "id < ?"]
        constraints += ["(LOWER(title) LIKE ? OR LOWER(summary) LIKE ?)" for _ in search_words]
        constraints += ["tags REGEXP ?" for _ in tags]
        if len(disabled_feeds) > 0:
            constraints.append(f"feed_id NOT IN (" + ", ".join(["?" for _ in disabled_feeds]) + ")")

        query = (
            "SELECT * FROM gists " +
            "WHERE " + " AND ".join(constraints) + " " +
            "ORDER BY id DESC LIMIT ?"
        )
        received_query = query
        for param in params:
            received_query = received_query.replace("?", str(param), 1)
        self._logger.info(f"Received query: {received_query}")

        try:
            with self._connection.cursor() as cur:
                cur.execute(query, params)
                return [self.query_response_to_gist(response) for response in cur.fetchall()]
        except mariadb.Error as e:
            self._logger.error(
                f"Error getting older gists with query '{query}', last_id {last_id}, take {take}, tags {tags}", 
                exc_info=True
            )
            raise e
        
    def get_gist_by_reference(self, reference: str) -> Gist | None:
        query = "SELECT * FROM gists WHERE reference = ? AND disabled IS FALSE"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (reference,))
                result = cur.fetchone()
                return None if result is None else self.query_response_to_gist(result)
        except mariadb.Error as e:
            self._logger.error(f"Error getting gist with reference '{reference}'", exc_info=True)
            raise e
    
    @staticmethod
    def query_response_to_feed_info(response: tuple) -> FeedInfo:
        id, title, link, rss_link, language = response
        return FeedInfo(id, title, link, rss_link, language)
    
    def get_all_feed_info(self) -> list[FeedInfo]:
        query = "SELECT * FROM feeds"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query)
                return [self.query_response_to_feed_info(response) for response in cur.fetchall()]
        except mariadb.Error as e:
            self._logger.error(
                f"Error getting older gists with query '{query}'", 
                exc_info=True
            )
            raise e
