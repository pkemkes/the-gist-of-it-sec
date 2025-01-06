from bs4 import BeautifulSoup
from dataclasses import dataclass
from typing import Callable


MISSING_CONTAINER_MSG = "Could not find text container"
NO_TEXT_IN_CONTAINER_MSG = "Could not find text container"


@dataclass
class FeedDefinition:
    rss_url: str
    extract_text: Callable[[str], str]
    categories: list[str] | None


def extract_text_krebs_on_security(page: str) -> str:
    soup = BeautifulSoup(page, "html.parser")
    entry_content = soup.find("div", { "class": "entry-content" })
    if entry_content is None:
        raise RuntimeError(MISSING_CONTAINER_MSG)
    entry_texts = entry_content.find_all(string=True)
    if len(entry_texts) == 0:
        raise RuntimeError(NO_TEXT_IN_CONTAINER_MSG)
    return "".join(entry_texts).strip()


def extract_text_bleeping_computer(page: str) -> str:
    soup = BeautifulSoup(page, "html.parser")
    entry_content = soup.find("div", { "class": "articleBody" })
    if entry_content is None:
        raise RuntimeError(MISSING_CONTAINER_MSG)
    entry_texts = entry_content.find_all(string=True)
    if len(entry_texts) == 0:
        raise RuntimeError(NO_TEXT_IN_CONTAINER_MSG)
    result = "".join(entry_texts)
    trimmed = result.split("Related Articles:")
    return trimmed[0].strip() if len(trimmed) > 0 else result


def extract_text_dark_reading(page: str) -> str:
    soup = BeautifulSoup(page, "html.parser")
    entry_content = soup.find("div", { "class": "ArticleBase-BodyContent" })
    if entry_content is None:
        raise RuntimeError(MISSING_CONTAINER_MSG)
    entry_texts = entry_content.find_all(string=True)
    if len(entry_texts) == 0:
        raise RuntimeError(NO_TEXT_IN_CONTAINER_MSG)
    return "".join(entry_texts).strip()


def extract_text_the_verge(page: str) -> str:
    soup = BeautifulSoup(page, "html.parser")
    entry_content = soup.find("div", { "class": "duet--article--article-body-component-container" })
    if entry_content is None:
        raise RuntimeError(MISSING_CONTAINER_MSG)
    entry_texts = entry_content.find_all(string=True)
    if len(entry_texts) == 0:
        raise RuntimeError(NO_TEXT_IN_CONTAINER_MSG)
    return "".join(entry_texts).strip()


def extract_text_gdata(page: str) -> str:
    soup = BeautifulSoup(page, "html.parser")
    entry_content = soup.find("div", { "class": "nm-article-blog" })
    if entry_content is None:
        raise RuntimeError(MISSING_CONTAINER_MSG)
    entry_texts = entry_content.find_all(string=True)
    if len(entry_texts) == 0:
        raise RuntimeError(NO_TEXT_IN_CONTAINER_MSG)
    return "".join(entry_texts).strip()    


def extract_text_the_record(page: str) -> str:
    soup = BeautifulSoup(page, "html.parser")
    article_content = soup.find("div", { "class": "article__content" })
    entry_content = article_content.findChildren("span", { "class": "wysiwyg-parsed-content" })
    if entry_content is None or len(entry_content) < 1:
        raise RuntimeError(MISSING_CONTAINER_MSG)
    entry_texts = entry_content[0].find_all(string=True)
    if len(entry_texts) == 0:
        raise RuntimeError(NO_TEXT_IN_CONTAINER_MSG)
    return "".join(entry_texts).strip()


feed_definitions = [
    FeedDefinition(
        "https://krebsonsecurity.com/feed",
        extract_text_krebs_on_security,
        None
    ),
    FeedDefinition(
        "https://www.bleepingcomputer.com/feed/",
        extract_text_bleeping_computer,
        [ "Security" ]
    ),
    FeedDefinition(
        "https://www.darkreading.com/rss.xml",
        extract_text_dark_reading,
        None
    ),
    FeedDefinition(
        "https://www.theverge.com/rss/cyber-security/index.xml",
        extract_text_the_verge,
        None
    ),
    FeedDefinition(
        "https://feeds.feedblitz.com/GDataSecurityBlog-EN&x=1",
        extract_text_gdata,
        None
    ),
    FeedDefinition(
        "https://therecord.media/feed",
        extract_text_the_record,
        None
    )
]
