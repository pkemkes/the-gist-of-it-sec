import feedparser
import requests
from typing import Callable, Any
from time import mktime
from datetime import datetime, timezone

from gists_utils.types import Gist, AIResponse
from gists_utils.logger import get_logger


class RSSEntry:
    def __init__(self, entry_dict: feedparser.FeedParserDict, feed_id: str, 
                 extract_text: Callable[[str], str]):
        self._entry_dict = entry_dict
        self.reference: str = self._extract("id")
        self.feed_id = feed_id
        self.author: str = self._extract("author", "")
        self.title: str = self._extract("title")
        self.published: datetime = self._parse_datetime("published_parsed")
        self.updated: datetime = self._parse_datetime("updated_parsed")
        self.link: str = self._extract("link")
        self.categories: list[str] = self._extract_categories()
        self._extract_text_func: Callable = extract_text
        self.text_content: str | None
        self.logger = get_logger("feed_entry")
        self.dummy_user_agent = "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:131.0) Gecko/20100101 Firefox/131.0"
        self.requests_headers = {
            "User-Agent": self.dummy_user_agent
        }

    def _extract(self, field: str, default: any = None) -> Any:
        extracted = self._entry_dict.get(field, default)
        if extracted is None:
            raise KeyError(f"Entry has no {field}")
        return extracted
    
    def _parse_datetime(self, key: str) -> datetime:
        published_parsed = self._extract(key)
        return datetime.fromtimestamp(mktime(published_parsed), timezone.utc)
    
    def _fetch_page(self) -> str:
        try:
            resp = requests.get(self.link, headers=self.requests_headers)
            if not resp.ok:
                raise RuntimeError(f"Error while getting {self.link}. Code: {resp.status_code}")
            return resp.content.decode("utf-8")
        except requests.RequestException as e:
            self.logger.error(f"Could not get {self.link}. Error: {e}")
            raise e
    
    def fetch_text(self) -> None:
        self.text_content = self._extract_text_func(self._fetch_page())
    
    def _extract_categories(self) -> list[str]:
        tags = self._extract("tags", [])
        return [tag.get("term") for tag in tags]
    
    def to_gist(self, ai_response: AIResponse) -> Gist:
        return Gist(
            None,
            self.reference,
            self.feed_id,
            self.author,
            self.title,
            self.published,
            self.updated,
            self.link,
            ai_response.summary,
            ai_response.tags,
            ai_response.search_query
        )
