from dataclasses import dataclass
from datetime import datetime


@dataclass
class FeedInfo:
    id: int | None
    title: str
    link: str
    rss_link: str
    language: str


@dataclass
class Gist:
    id: int | None
    reference: str
    feed_id: int
    author: str
    title: str
    published: datetime
    updated: datetime
    link: str
    summary: str
    tags: list[str]
    search_query: str


@dataclass
class SearchResult:
    id: int | None
    gist_id: int
    title: str
    snippet: str 
    link: str
    display_link: str
    thumbnail_link: str | None
    image_link: str | None


@dataclass
class AIResponse:
    summary: str
    tags: list[str]
    search_query: str
