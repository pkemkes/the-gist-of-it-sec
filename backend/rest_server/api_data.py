from dataclasses import dataclass
from typing import List


@dataclass
class GistApiResponse:
    id: int
    feed_title: str
    feed_link: str
    author: str
    title: str
    published: str
    updated: str
    link: str
    summary: str
    tags: List[str]
    search_query: str


@dataclass
class SimilarGistApiResponse:
    gist: GistApiResponse
    similarity: float


@dataclass
class FeedInfoApiResponse:
    id: int
    title: str
    link: str
    language: str


@dataclass
class RelatedGistInRecap:
    id: int
    title: str


@dataclass
class RecapCategoryApiResponse:
    heading: str
    recap: str
    related: list[RelatedGistInRecap]


@dataclass
class RecapApiResponse:
    recap: list[RecapCategoryApiResponse]
    created: str
    