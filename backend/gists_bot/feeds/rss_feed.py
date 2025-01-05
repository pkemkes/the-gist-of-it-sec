import feedparser
from typing import List, Any
from abc import ABC, abstractmethod

from gists_utils.types import FeedInfo
from feeds.rss_entry import RSSEntry


class RSSFeed(ABC):
    def __init__(self):
        self.parsed_feed: feedparser.FeedParserDict | None
        self.entries: List[RSSEntry] | None
        self.parse_feed()
    
    @property
    def id(self) -> int:
        return self._id
    
    def set_id(self, id: int):
        self._id = id

    @property
    def rss_url(self) -> str:
        return self._rss_url
    
    @property
    def title(self) -> str:
        return self._extract("title")
    
    @property
    def link(self) -> str:
        return self._extract("link")

    @property
    def language(self) -> str:
        return self._extract("language")
        
    @property
    def feed_info(self) -> FeedInfo:
        return self._to_feed_info()

    def _extract(self, field: str) -> Any:
        extracted = self.parsed_feed.feed.get(field)
        if extracted is None:
            raise KeyError(f"Feed has no {field}")
        return extracted
    
    def _to_feed_info(self) -> FeedInfo:
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
            RSSEntry(entry, self.id, self._extract_text)
            for entry in self.parsed_feed.entries
        ]
    
    @staticmethod
    @abstractmethod
    def _extract_text(page: str) -> str:
        raise NotImplementedError
