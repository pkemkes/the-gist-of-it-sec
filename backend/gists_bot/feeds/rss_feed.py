import feedparser
from typing import List, Any
from html import unescape

from gists_utils.types import FeedInfo
from feeds.rss_entry import RSSEntry
from feeds.feeds import FeedDefinition


class RSSFeed():
    def __init__(self, feed_definition: FeedDefinition):
        self.parsed_feed: feedparser.FeedParserDict | None
        self.entries: List[RSSEntry] | None
        self.id: int | None = None
        self.rss_url = feed_definition.rss_url
        self.categories = feed_definition.categories
        self.extract_text = feed_definition.extract_text
        self.parse_feed()
        self.title = unescape(self._extract("title"))
        self.link = self._extract("link")
        self.language = self._extract("language")

    def set_id(self, id: int):
        self.id = id

    def _extract(self, field: str) -> Any:
        extracted = self.parsed_feed.feed.get(field)
        if extracted is None:
            raise KeyError(f"Feed has no {field}")
        return extracted
    
    def to_feed_info(self) -> FeedInfo:
        return FeedInfo(
            self.id,
            self.title,
            self.link,
            self.rss_url,
            self.language
        )
    
    def parse_feed(self) -> None:
        self.parsed_feed = feedparser.parse(self.rss_url)
        self.entries = [
            RSSEntry(entry, self.id, self.extract_text)
            for entry in self.parsed_feed.entries
        ]

    def is_correct_category(self, entry: RSSEntry) -> bool:
        if self.categories is None:
            return True
        return all(defined_cat in entry.categories for defined_cat in self.categories)
