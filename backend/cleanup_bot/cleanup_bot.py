from time import sleep
import feedparser
import requests
from requests.adapters import HTTPAdapter, Retry
from prometheus_client import Gauge, Summary

from mariadb_cleanup_handler import MariaDbCleanupHandler
from chromadb_cleanup_handler import ChromaDbCleanupHandler
from gists_utils.logger import get_logger
from gists_utils.types import Gist


CLEANUP_GISTS_GAUGE = Gauge("cleanup_gists_seconds", "Time spent to cleanup all gists")
CHECK_GIST_SUMMARY = Summary("check_gist_seconds", "Time spent to check a single gist", [ "feed_title" ])
ENSURE_CORRECT_DISABLED_SUMMARY = Summary(
    "ensure_correct_disabled_seconds", 
    "Time spent to ensure that a single gist has the correct disabled state",
    [ "feed_title", "should_be_disabled", "is_disabled" ]
)
ENSURE_CHROMADB_SUMMARY = Summary(
    "ensure_chromadb_seconds",
    "Time spent to ensure that a single entry is present in the chroma db and has the correct metadata",
    [ "feed_title", "should_be_disabled", "is_disabled", "feed_id_in_db" ]
)


class CleanUpBot:
    def __init__(self):
        self._mariadb = MariaDbCleanupHandler()
        self._chromadb = ChromaDbCleanupHandler()
        self._logger = get_logger("cleanup_bot")
        self._feeds: dict[int, any] | None = None
        dummy_user_agent = "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:131.0) Gecko/20100101 Firefox/131.0"
        self._headers = { "User-Agent": dummy_user_agent }
    
    @CLEANUP_GISTS_GAUGE.time()
    def cleanup_gists(self) -> None:
        gists = self._mariadb.get_all_gists()
        self._fetch_feeds(gists)
        for gist in gists:
            self._check_gist(gist)
            sleep(0.2)
        self._logger.info("Cleanup search done!")
    
    def _fetch_feeds(self, gists: list[Gist]) -> dict[int, any]:
        feed_ids = set(gist.feed_id for gist in gists)
        feed_infos_by_ids = {
            feed_id: self._mariadb.get_feed_by_id(feed_id) for feed_id in feed_ids
        }
        self._feeds = {
            feed_id: feedparser.parse(feed_info.rss_link) 
            for feed_id, feed_info in feed_infos_by_ids.items()
        }

    def _check_gist(self, gist: Gist) -> None:
        feed_title = self._feeds[gist.feed_id].feed.get("title")
        with CHECK_GIST_SUMMARY.labels(feed_title).time():
            should_be_disabled = self._gist_should_be_disabled(gist)
            self._ensure_correct_disabled_state(gist, should_be_disabled)
            self._ensure_chromadb_completeness(gist, should_be_disabled)

    def _ensure_correct_disabled_state(self, gist: Gist, should_be_disabled: bool) -> None:
        is_disabled = self._mariadb.gist_is_disabled(gist)
        feed_title = self._feeds[gist.feed_id].feed.get("title")
        with ENSURE_CORRECT_DISABLED_SUMMARY.labels(feed_title, should_be_disabled, is_disabled).time():
            if should_be_disabled and not is_disabled:
                self._mariadb.disable_gist(gist)
                self._chromadb.disable_gist(gist)
                self._logger.info(f"Disabled gist with id {gist.id} and link {gist.link}")
            if is_disabled and not should_be_disabled:
                self._mariadb.enable_gist(gist)
                self._chromadb.enable_gist(gist)
                self._logger.info(f"Enabled gist with id {gist.id} and link {gist.link}")
    
    def _ensure_chromadb_completeness(self, gist: Gist, should_be_disabled: bool) -> None:
        metadata = self._chromadb.get_metadata(gist.reference)
        is_disabled = metadata.get(self._chromadb.disabled_key)
        feed_id = metadata.get(self._chromadb.feed_id_key)
        feed_title = self._feeds[gist.feed_id].feed.get("title")
        with ENSURE_CHROMADB_SUMMARY.labels(
            feed_title, should_be_disabled, is_disabled, feed_id
        ).time():
            if is_disabled != should_be_disabled or feed_id is None:
                feed_id = self._mariadb.get_feed_id_by_gist_reference(gist.reference)
                new_metadata = { 
                    self._chromadb.reference_key: gist.reference,
                    self._chromadb.disabled_key: should_be_disabled,
                    self._chromadb.feed_id_key: self._mariadb.get_feed_id_by_gist_reference(gist.reference)
                }
                self._chromadb.set_metadata(gist.reference, new_metadata)
                self._logger.info(f"Set new metadata in chromadb on gist with reference {gist.reference}")
    
    def _gist_should_be_disabled(self, gist: Gist) -> bool:
        session = self._get_session()
        resp = session.get(gist.link, headers=self._headers, allow_redirects=False)
        if resp.status_code == 404:
            return True
        if resp.is_redirect:
            if gist.link not in self._get_entry_links_for_feed(self._feeds[gist.feed_id]):
                return True
        return False

    @staticmethod
    def _get_session() -> requests.Session:
        session = requests.Session()
        retry = Retry(total=3, backoff_factor=1, status_forcelist=[ 500, 502, 503, 504 ])
        session.mount("http://", HTTPAdapter(max_retries=retry))
        session.mount("https://", HTTPAdapter(max_retries=retry))
        return session

    @staticmethod
    def _get_entry_links_for_feed(feed: any) -> list[str]:
        return [entry.get("link") for entry in feed.entries]
