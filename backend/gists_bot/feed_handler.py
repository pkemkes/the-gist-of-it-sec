from uuid import uuid4
from prometheus_client import Summary
from time import time

from gists_utils.logger import get_logger
from gists_utils.types import Gist, SearchResult
from mariadb_gists_handler import MariaDbGistsHandler
from openai_handler import OpenAIHandler
from chromadb_inserter import ChromaDbInserter
from google_search_handler import GoogleSearchHandler
from feeds.feeds import FeedDefinition
from feeds.rss_feed import RSSFeed
from feeds.rss_entry import RSSEntry


PROCESS_ENTRY_SUMMARY = Summary("process_entry_seconds", "Time spent processing a single entry",
                                [ "feed_title" ])
UPSERT_GIST_SUMMARY = Summary("upsert_gist_seconds", "Time spent upserting a single gist", 
                              [ "previous_version_exists", "feed_title" ])
UPSERT_SEARCH_RESULT_SUMMARY = Summary(
    "upsert_search_result_seconds", 
    "Time spent upserting a single set of search results", 
    [ "previous_version_exists", "feed_title" ]
)


class FeedHandler:
    def __init__(self, db: MariaDbGistsHandler, ai: OpenAIHandler, chromadb: ChromaDbInserter,
                 google: GoogleSearchHandler, feed_definition: FeedDefinition):
        self._db = db
        self._ai = ai
        self._chromadb = chromadb
        self._google = google
        self._feed = RSSFeed(feed_definition)
        self.store_feed_info_if_not_present()
        self._logger = get_logger(f"feed_handler_{self._feed.id}")
        dummyEmptyString = "DUMMYEMPTYSEARCHRESULT"
        self._dummy_empty_search_result = lambda gist_id: [SearchResult(
            None,
            gist_id,
            dummyEmptyString,
            dummyEmptyString,
            dummyEmptyString,
            dummyEmptyString,
            None,
            None
        )]
    
    def store_feed_info_if_not_present(self) -> None:
        logger = get_logger(f"feed_handler_{str(uuid4())[:8]}")
        feed_id = self._db.get_feed_id_by_link(self._feed.link)
        if feed_id is None:
            self._db.insert_feed_info(self._feed.to_feed_info())
            feed_id = self._db.get_feed_id_by_link(self._feed.link)
            logger.info(f"Stored feed info of feed with link {self._feed.link} and id {feed_id}")
        self._feed.set_id(feed_id)
    
    def upsert_gist(self, gist: Gist, previous_version_exists: bool) -> None:
        with UPSERT_GIST_SUMMARY.labels(previous_version_exists, self._feed.title).time():
            gist.id = self._db.update_gist(gist) if previous_version_exists else self._db.insert_gist(gist)
    
    def upsert_search_results(self, results: list[SearchResult], gist_id: int, previous_version_exists: bool) -> None:
        with UPSERT_SEARCH_RESULT_SUMMARY.labels(previous_version_exists, self._feed.title).time():
            if previous_version_exists:
                self._db.update_search_results(results, gist_id)
            else:
                self._db.insert_search_results(results, gist_id)

    def get_and_insert_search_results(self, gist: Gist, previous_version_exists: bool) -> None:
        results = self._google.get_search_results(gist)
        if results is None:
            return
        if len(results) == 0:
            results = self._dummy_empty_search_result
        self.upsert_search_results(results, gist.id, previous_version_exists)

    def check_for_potentially_missing_search_results(self, entry: RSSEntry) -> None:
        # This is necessary because we might run out of free tokens for the Google search.
        # In those cases the search fails and the gist is inserted without the results.
        # With this check the search query will be retried every time until any results are inserted.
        # Note: This is quite hacky and might be a problem if a search legitimately returns no results.
        #       This can only really be solved by paying for the API though...
        gist_id = self._db.get_gist_id_by_reference(entry.reference)
        if gist_id is None:
            return
        results = self._db.get_search_results_by_gist_id(gist_id)
        if len(results) != 0:
            return
        self._logger.info(f"Gist with id {gist_id} does not have search results. Trying to search and insert...")
        gist = self._db.get_gist_by_id(gist_id)
        self.get_and_insert_search_results(gist, False)

    def process_entry(self, entry: RSSEntry) -> None:
        start_time = time()
        if not self._feed.is_correct_category(entry):
            return
        updated = self._db.get_gist_updated_by_reference_if_exists(entry.reference)
        if updated == entry.updated:  # Current version already exists in db
            self.check_for_potentially_missing_search_results(entry)
            return
        previous_version_exists = updated is not None  # If we found any gist in db at all, it must be outdated
        entry.fetch_text()
        ai_response = self._ai.summarize_entry(entry)
        gist = entry.to_gist(ai_response)
        self.upsert_gist(gist, previous_version_exists)
        self._chromadb.add_or_update_entry(entry)
        self.get_and_insert_search_results(gist, previous_version_exists)
        self._logger.info(
            ("Updated " if previous_version_exists else "Inserted new ") + 
            f"gist with reference {gist.reference} and id {gist.id}"
        )
        PROCESS_ENTRY_SUMMARY.labels(self._feed.title).observe(time() - start_time)

    def get_entries(self) -> list[RSSEntry]:
        self._feed.parse_feed()
        return self._feed.entries
