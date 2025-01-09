import requests
from requests.adapters import HTTPAdapter, Retry
import feedparser
from logging import Logger
from time import sleep

from mariadb_cleanup_handler import MariaDbCleanupHandler
from gists_utils.types import Gist
from gists_utils.run_in_loop import run_in_loop
from gists_utils.logger import get_logger


DUMMY_USER_AGENT = "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:131.0) Gecko/20100101 Firefox/131.0"
HEADERS = { "User-Agent": DUMMY_USER_AGENT }


def get_session() -> requests.Session:
	session = requests.Session()
	retry = Retry(total=3, backoff_factor=1, status_forcelist=[ 500, 502, 503, 504 ])
	session.mount("http://", HTTPAdapter(max_retries=retry))
	session.mount("https://", HTTPAdapter(max_retries=retry))
	return session


def get_entry_links_for_feed(feed: any) -> list[str]:
	return [entry.get("link") for entry in feed.entries]


def gist_should_be_disabled(gist: Gist, feeds: dict[int, any]) -> bool:
	session = get_session()
	resp = session.get(gist.link, headers=HEADERS, allow_redirects=False)
	if resp.status_code == 404:
		return True
	if resp.is_redirect:
		if gist.link not in get_entry_links_for_feed(feeds[gist.feed_id]):
			return True
	return False


def get_feeds(db: MariaDbCleanupHandler, gists: list[Gist]) -> dict[int, any]:
	feed_ids = set(gist.feed_id for gist in gists)
	feed_infos_by_ids = {feed_id: db.get_feed_by_id(feed_id) for feed_id in feed_ids}
	return {
		feed_id: feedparser.parse(feed_info.rss_link) 
		for feed_id, feed_info in feed_infos_by_ids.items()
	}


def cleanup_gists(db: MariaDbCleanupHandler, logger: Logger) -> None:
	gists = db.get_all_gists()
	feeds = get_feeds(db, gists)
	for gist in gists:
		should_be_disabled = gist_should_be_disabled(gist, feeds)
		is_disabled = db.gist_is_disabled(gist)
		if should_be_disabled and not is_disabled:
			db.set_disable_state_of_gist(True, gist)
			logger.info(f"Disabled gist with id {gist.id} and link {gist.link}")
		if is_disabled and not should_be_disabled:
			db.set_disable_state_of_gist(False, gist)
			logger.info(f"Enabled gist with id {gist.id} and link {gist.link}")
		sleep(0.2)
	logger.info("Cleanup search done!")


def main():
	db = MariaDbCleanupHandler()
	logger = get_logger("cleanup_bot")
	run_in_loop(cleanup_gists, [db, logger], 60*10)


if __name__ == "__main__":
	main()
