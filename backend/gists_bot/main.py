from time import time, sleep
from typing import Callable, List, Any
from os import getenv

from gists_utils.logger import get_logger
from mariadb_gists_handler import MariaDbGistsHandler
from openai_handler import OpenAIHandler
from chromadb_inserter import ChromaDbInserter
from google_search_handler import GoogleSearchHandler
from feed_handler import FeedHandler
from feeds.rss_entry import RSSEntry
from feeds.feeds import (
    KrebsOnSecurity,
    BleepingComputer,
    DarkReading,
    TheVergeCybersecurity
)


def run_in_loop(
        to_be_run: Callable[[Any], None],
        args: list, timeframe: float = 60
    ) -> None:
    while True:
        started = time()
        to_be_run(*args)
        next_execution = started + timeframe
        now = time()
        if now < next_execution:
            sleep(next_execution - now)


def process_entries(feed_handlers: List[FeedHandler], mode: str) -> None:
    logger = get_logger("gists_bot")
    entries: list[RSSEntry] = []
    for feed_handler in feed_handlers:
        entries += feed_handler.get_entries()
    entries.sort(key=lambda entry: entry.updated)
    if mode == "dev":
        logger.warning("Running in dev mode! Only processing the 10 newest entries.")
        entries = entries[-10:]
    for entry in entries:
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
    feeds = [
        KrebsOnSecurity(),
        BleepingComputer(),
        DarkReading(),
        TheVergeCybersecurity()
    ]
    feed_handlers = [ FeedHandler(db, ai, chroma, google, feed) for feed in feeds ]

    run_in_loop(process_entries, [feed_handlers, mode])


if __name__ == "__main__":
    main()
