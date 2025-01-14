import mariadb

from gists_utils.mariadb_handler import MariaDbHandler
from gists_utils.types import Gist, SearchResult


class MariaDbCleanupHandler(MariaDbHandler):
    def __init__(self):
        super().__init__()

    def get_all_gists(self) -> list[Gist]:
        query = "SELECT * FROM gists"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query)
                return [self.query_response_to_gist(response) for response in cur.fetchall()]
        except mariadb.Error as e:
            self._logger.error("Error getting all gists from database", exc_info=True)
            raise e
        
    def gist_is_disabled(self, gist: Gist) -> bool:
        query = "SELECT disabled FROM gists WHERE id = ?"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (gist.id, ))
                return cur.fetchone()[0]
        except mariadb.Error as e:
            self._logger.error(f"Error getting disabled state gist with id {gist.id}", exc_info=True)
            raise e
    
    def _set_disable_state_of_gist(self, disabled: bool, gist: Gist) -> None:
        query = "UPDATE gists SET disabled = ? WHERE id = ?"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (disabled, gist.id, ))
                affected_gists = cur.rowcount
                if affected_gists < 1:
                    self._logger.error(f"Could not set disabled state of gist with id {gist.id}")
                if affected_gists > 1:
                    raise RuntimeError(f"Set disabled state for more than one gist with id {gist.id}")
        except mariadb.Error as e:
            self._logger.error(f"Error when trying to set disabled state of gist with id {gist.id}", exc_info=True)
            raise e
    
    def disable_gist(self, gist: Gist) -> None:
        self._set_disable_state_of_gist(True, gist)
    
    def enable_gist(self, gist: Gist) -> None:
        self._set_disable_state_of_gist(False, gist)
    
    def get_feed_id_by_gist_reference(self, reference: str) -> int:
        query = "SELECT feed_id FROM gists WHERE reference = ?"
        try:
            with self._connection.cursor() as cur:
                cur.execute(query, (reference, ))
                result = cur.fetchall()
                if len(result) < 1:
                    raise Exception(f"Did not find gist with reference {reference}")
                if len(result) > 1:
                    raise Exception(f"Found more than one gist with reference {reference}")
                return result[0][0]
        except mariadb.Error as e:
            self._logger.error(
                f"Error when trying to get feed id for gist with reference {reference}",
                exc_info=True
            )
            raise e
