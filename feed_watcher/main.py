from os import getenv
from datetime import datetime, timedelta, timezone
from time import sleep, mktime
import json
from feedparser import parse, FeedParserDict


FEEDS = [
	"https://krebsonsecurity.com/feed",
	"https://www.bleepingcomputer.com/feed/",
	"https://www.darkreading.com/rss.xml",
	"https://www.theverge.com/rss/cyber-security/index.xml",
	"https://feeds.feedblitz.com/GDataSecurityBlog-EN&x=1",
	"https://therecord.media/feed",
	"https://feeds.arstechnica.com/arstechnica/technology-lab", 
	"https://www.heise.de/security/feed.xml",
	"https://www.security-insider.de/rss/news.xml"
]
LOGPATH = getenv("LOGPATH", "feed_watcher.log")
DELAY_SECONDS = timedelta(seconds=int(getenv("DELAY_SECONDS", "60")))

NEWEST_ENTRIES_BY_FEED: dict[str, str] = {}

def main():
	while True:
		start_time = get_now()
		for feed_url in FEEDS:
			watch_feed(feed_url)
		seconds_to_sleep = DELAY_SECONDS.total_seconds() - (get_now() - start_time).total_seconds()
		if seconds_to_sleep > 0:
			print(f"Sleeping for {seconds_to_sleep} seconds...")
			sleep(seconds_to_sleep)
		else:
			print("Took longer than delay interval, not sleeping.")

def get_now() -> datetime:
	return datetime.now(timezone.utc)

def watch_feed(url: str):
	feed = parse(url)
	newest_entry = get_newest_entry(feed)
	if not newest_entry:
		print(f"No entries found in feed: {url}")
		return
	stored_newest_id = NEWEST_ENTRIES_BY_FEED.get(url)
	if stored_newest_id is None:
		NEWEST_ENTRIES_BY_FEED[url] = newest_entry.id
		print(f"Initialized newest entry for feed: {url} with ID: {newest_entry.id}")
		return
	if stored_newest_id == newest_entry.id:
		return
	print(f"New entry found in feed: {url}")
	NEWEST_ENTRIES_BY_FEED[url] = newest_entry.id
	log_entry_delay(url, newest_entry)

def parse_timestamp(timestamp: str) -> datetime:
	return datetime.fromtimestamp(mktime(timestamp), timezone.utc)

def get_newest_entry(feed: FeedParserDict) -> FeedParserDict | None:
	if not feed.entries:
		return None
	newest_entry = max(feed.entries, key=lambda e: parse_timestamp(e.updated_parsed))
	return newest_entry

def log_entry_delay(feed_url: str, entry: FeedParserDict):
	delay = get_entry_delay(entry)
	with open(LOGPATH, "a", encoding="utf-8") as f:
		log = {
			"feed": feed_url,
			"entry_id": entry.id,
			"entry_url": entry.link,
			"delay_seconds": delay.total_seconds(),
		}
		json.dump(log, f)
		f.write("\n")

def get_entry_delay(entry: FeedParserDict) -> timedelta:
	entry_time = parse_timestamp(entry.updated_parsed)
	return get_now() - entry_time

if __name__ == "__main__":
	main()