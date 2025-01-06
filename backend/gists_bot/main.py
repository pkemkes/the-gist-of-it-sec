from os import getenv

from gists_utils.logger import get_logger
from gists_utils.run_in_loop import run_in_loop
from mariadb_gists_handler import MariaDbGistsHandler
from openai_handler import OpenAIHandler
from chromadb_inserter import ChromaDbInserter
from google_search_handler import GoogleSearchHandler
from feed_handler import FeedHandler
from feeds.rss_entry import RSSEntry
from feeds.feeds import feed_definitions


def process_entries(feed_handlers: list[FeedHandler], mode: str) -> None:
    logger = get_logger("gists_bot")
    entries_and_feed_handlers: list[tuple[RSSEntry, FeedHandler]] = []
    for feed_handler in feed_handlers:
        entries_and_feed_handlers += [(entry, feed_handler) for entry in  feed_handler.get_entries()]
    entries_and_feed_handlers.sort(key=lambda entry_and_feed_handler: entry_and_feed_handler[0].updated)
    if mode == "dev":
        logger.warning("Running in dev mode! Only processing the 10 newest entries.")
        entries_and_feed_handlers = entries_and_feed_handlers[-10:]
    for entry, feed_handler in entries_and_feed_handlers:
        try:
            feed_handler.process_entry(entry)
        except Exception:
            logger.error(f"Error when processing entry with reference {entry.reference}", exc_info=True)


def main():
    mode = getenv("APP_MODE", "prod")
    db = MariaDbGistsHandler()
    ai = OpenAIHandler()
    chroma = ChromaDbInserter()
    google = GoogleSearchHandler(db)
    feed_handlers = [ 
        FeedHandler(db, ai, chroma, google, feed_definition)
        for feed_definition in feed_definitions
    ]

    run_in_loop(process_entries, [feed_handlers, mode], 60)


if __name__ == "__main__":
    main()
