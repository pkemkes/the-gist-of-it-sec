import mariadb
from datetime import datetime, timezone, timedelta
import json
from dataclasses import asdict

from gists_utils.types import Gist, FeedInfo, SearchResult, CategoryRecap
from gists_utils.mariadb_handler import MariaDbHandler


class MariaDbGistsHandler(MariaDbHandler):
    def __init__(self):
        super().__init__()
    
    def get_gist_updated_by_reference_if_exists(self, reference: str) -> datetime | None:
        try:
            with self._connection.cursor() as cur:
                cur.execute("SELECT updated FROM gists WHERE reference = ?", (reference,))
                results = cur.fetchall()
        except mariadb.Error as e:
            self._logger.error(f"Error checking if gist exists with reference {reference}", exc_info=True)
            raise e
        if len(results) > 1:
            raise KeyError(f"Found multiple gists with reference {reference}")
        return None if len(results) == 0 else results[0][0].replace(tzinfo=timezone.utc)
    
    def get_feed_id_by_link(self, link: str) -> int | None:
        try:
            with self._connection.cursor() as cur:
                cur.execute("SELECT id FROM feeds WHERE link = ?", (link,))
                results = cur.fetchall()
        except mariadb.Error as e:
            self._logger.error(f"Error getting feed with link {link}", exc_info=True)
            raise e
        if len(results) == 1:
            return results[0][0]
        if len(results) == 0:
            return None
        self._logger.error(f"Found multiple feeds with link {link}" )
        raise KeyError(link)
    
    def insert_feed_info(self, feed: FeedInfo) -> None:
        query = (
            "INSERT INTO feeds (title, link, rss_link, language) "
            "VALUES (?, ?, ?, ?)"
        )
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (
                    feed.title,
                    feed.link,
                    feed.rss_link,
                    feed.language
                ))
        except mariadb.Error as e:
            self._logger.error("Error inserting feed into databse", exc_info=True)
            raise e
    
    def get_gist_id_by_reference(self, reference: str) -> int | None:
        query = "SELECT id FROM gists WHERE reference = ?"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (reference, ))
                results = cur.fetchall()
        except mariadb.Error as e:
            self._logger.error(
                f"Error getting gist from database by reference {reference}",
                exc_info=True
            )
            raise e
        if len(results) > 1:
            self._logger.error(f"Found multiple gists with reference {reference}")
            raise KeyError(id)
        return None if len(results) == 0 else results[0][0]

    def insert_gist(self, gist: Gist) -> int:
        query = (
            "INSERT INTO gists "
            "(reference, feed_id, author, title, published, updated, link, summary, tags, search_query) "
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
        )
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (
                    gist.reference,
                    gist.feed_id,
                    gist.author,
                    gist.title,
                    gist.published,
                    gist.updated,
                    gist.link,
                    gist.summary,
                    ";;".join(gist.tags),
                    gist.search_query
                ))
                if cur.rowcount != 1:
                    self._logger.warning(f"Could not insert gist with reference {gist.reference}")
        except mariadb.Error as e:
            self._logger.error(
                f"Error inserting gist into database with reference {gist.reference}",
                exc_info=True
            )
            raise e
        return self.get_gist_id_by_reference(gist.reference)
        
    def update_gist(self, gist: Gist) -> int:
        query = (
            "UPDATE gists "
            "SET feed_id = ?, author = ?, title = ?, published = ?, "
                "updated = ?, link = ?, summary = ?, tags = ?, search_query = ? "
            "WHERE reference = ? AND disabled IS FALSE"
        )
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (
                    gist.feed_id,
                    gist.author,
                    gist.title,
                    gist.published,
                    gist.updated,
                    gist.link,
                    gist.summary,
                    ";;".join(gist.tags),
                    gist.search_query,
                    gist.reference
                ))
                if cur.rowcount != 1:
                    self._logger.warning(f"Could not update gist with reference {gist.reference}")
        except mariadb.Error as e:
            self._logger.error(
                f"Error updating gist in database with reference {gist.reference}",
                exc_info=True
            )
            raise e
        return self.get_gist_id_by_reference(gist.reference)
    
    def _insert_search_result(self, result: SearchResult, gist_id: int) -> None:
        query = (
            "INSERT INTO search_results "
            "(gist_id, title, snippet, link, display_link, thumbnail_link, image_link) "
            "VALUES (?, ?, ?, ?, ?, ?, ?)"
        )
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (
                    gist_id,
                    result.title,
                    result.snippet,
                    result.link,
                    result.display_link,
                    result.thumbnail_link,
                    result.image_link
                ))
                if cur.rowcount != 1:
                    self._logger.warning(f"Could not insert search result with link {result.link} for gist with id {gist_id}")
        except mariadb.Error as e:
            self._logger.error(
                f"Error inserting search result with link {result.link} for gist with id {gist_id}",
                exc_info=True
            )
            raise e

    def insert_search_results(self, results: list[SearchResult], gist_id: int) -> None:
        self._connection.autocommit = False
        for result in results:
            self._insert_search_result(result, gist_id)
        self._connection.commit()
        self._connection.autocommit = True
    
    def _delete_search_results(self, gist_id: int) -> None:
        query = "DELETE FROM search_results WHERE gist_id = ?"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (gist_id, ))
                if cur.rowcount < 1:
                    self._logger.warning(f"Could not delete any search results for gist with id {gist_id}")
        except mariadb.Error as e:
            self._logger.error(
                f"Error deleting search results for gist with id {gist_id}",
                exc_info=True
            )
            raise e
    
    def update_search_results(self, results: list[SearchResult], gist_id: int) -> None:
        self._connection.autocommit = False
        self._delete_search_results(gist_id)
        for result in results:
            self._insert_search_result(result, gist_id)
        self._connection.commit()
        self._connection.autocommit = True
    
    def get_last_daily_recap_created(self) -> datetime:
        return self._get_last_recap_created("daily")
    
    def get_last_weekly_recap_created(self) -> datetime:
        return self._get_last_recap_created("weekly")
    
    def _get_last_recap_created(self, recap_type: str) -> datetime:
        query = f"SELECT created FROM recaps_{recap_type} ORDER BY id DESC LIMIT 1"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query)
                result = cur.fetchone()
                if result is None:
                    return datetime.now(timezone.utc) - timedelta(days=365)
                return result[0].replace(tzinfo=timezone.utc)
        except mariadb.Error as e:
            self._logger.error(
                f"Error getting created for last {recap_type} recap",
                exc_info=True
            )
            raise e
    
    def get_gists_of_last_day(self) -> list[Gist]:
        return self._get_gists_of_last_days(1)
    
    def get_gists_of_last_week(self) -> list[Gist]:
        return self._get_gists_of_last_days(7)
        
    def _get_gists_of_last_days(self, days: int) -> list[Gist]:
        query = "SELECT * FROM gists WHERE updated >= ?"
        updated_after = datetime.now(timezone.utc) - timedelta(days)
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (updated_after,))
                return [self.query_response_to_gist(row) for row in cur.fetchall()]
        except mariadb.Error as e:
            self._logger.error(
                f"Error getting gists for last {days} days",
                exc_info=True
            )
            raise e
    
    def insert_daily_recap(self, recap: list[CategoryRecap]) -> None:
        self._insert_recap(recap, "daily")
    
    def insert_weekly_recap(self, recap: list[CategoryRecap]) -> None:
        self._insert_recap(recap, "weekly")
    
    def _insert_recap(self, recap: list[CategoryRecap], recap_type: str) -> None:
        query = f"INSERT INTO recaps_{recap_type} (created, recap) VALUES (?, ?)"
        created = datetime.now(timezone.utc)
        recap = json.dumps([asdict(category_recap) for category_recap in recap])
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (created, recap))
                if cur.rowcount != 1:
                    self._logger.warning(f"Could not insert following {recap_type} recap: {recap}")
        except mariadb.Error as e:
            self._logger.error(
                f"Error inserting following {recap_type} recap: {recap}",
                exc_info=True
            )
            raise e
