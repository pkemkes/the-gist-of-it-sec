from feeds.rss_feed import RSSFeed
from bs4 import BeautifulSoup


MISSING_CONTAINER_MSG = "Could not find text container"
NO_TEXT_IN_CONTAINER_MSG = "Could not find text container"


class KrebsOnSecurity(RSSFeed):
    def __init__(self):
        self._id = None
        self._rss_url = "https://krebsonsecurity.com/feed"
        super().__init__()
    
    @staticmethod
    def _extract_text(page: str) -> str:
        soup = BeautifulSoup(page, "html.parser")
        entry_content = soup.find("div", { "class": "entry-content" })
        if entry_content is None:
            raise RuntimeError(MISSING_CONTAINER_MSG)
        entry_texts = entry_content.find_all(string=True)
        if len(entry_texts) == 0:
            raise RuntimeError(NO_TEXT_IN_CONTAINER_MSG)
        return "".join(entry_texts).strip()


class BleepingComputer(RSSFeed):
    def __init__(self):
        self._id = None
        self._rss_url = "https://www.bleepingcomputer.com/feed/"
        super().__init__()
    
    @staticmethod
    def _extract_text(page: str) -> str:
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


class DarkReading(RSSFeed):
    def __init__(self):
        self._id = None
        self._rss_url = "https://www.darkreading.com/rss.xml"
        super().__init__()
    
    @staticmethod
    def _extract_text(page: str) -> str:
        soup = BeautifulSoup(page, "html.parser")
        entry_content = soup.find("div", { "class": "ArticleBase-BodyContent" })
        if entry_content is None:
            raise RuntimeError(MISSING_CONTAINER_MSG)
        entry_texts = entry_content.find_all(string=True)
        if len(entry_texts) == 0:
            raise RuntimeError(NO_TEXT_IN_CONTAINER_MSG)
        return "".join(entry_texts).strip()


class TheVergeCybersecurity(RSSFeed):
    def __init__(self):
        self._id = None
        self._rss_url = "https://www.theverge.com/rss/cyber-security/index.xml"
        super().__init__()
    
    @staticmethod
    def _extract_text(page: str) -> str:
        soup = BeautifulSoup(page, "html.parser")
        entry_content = soup.find("div", { "class": "duet--article--article-body-component-container" })
        if entry_content is None:
            raise RuntimeError(MISSING_CONTAINER_MSG)
        entry_texts = entry_content.find_all(string=True)
        if len(entry_texts) == 0:
            raise RuntimeError(NO_TEXT_IN_CONTAINER_MSG)
        return "".join(entry_texts).strip()


class GDATASecurityBlog(RSSFeed):
    def __init__(self):
        self._id = None
        self._rss_url = "https://feeds.feedblitz.com/GDataSecurityBlog-EN&x=1"
        super().__init__()
    
    @staticmethod
    def _extract_text(page: str) -> str:
        soup = BeautifulSoup(page, "html.parser")
        entry_content = soup.find("div", { "class": "nm-article-blog" })
        if entry_content is None:
            raise RuntimeError(MISSING_CONTAINER_MSG)
        entry_texts = entry_content.find_all(string=True)
        if len(entry_texts) == 0:
            raise RuntimeError(NO_TEXT_IN_CONTAINER_MSG)
        return "".join(entry_texts).strip()    


class TheRecord(RSSFeed):
    def __init__(self):
        self._id = None
        self._rss_url = "https://therecord.media/feed"
        super().__init__()
    
    @staticmethod
    def _extract_text(page: str) -> str:
        soup = BeautifulSoup(page, "html.parser")
        article_content = soup.find("div", { "class": "article__content" })
        entry_content = article_content.findChildren("span", { "class": "wysiwyg-parsed-content" })
        if entry_content is None or len(entry_content) < 1:
            raise RuntimeError(MISSING_CONTAINER_MSG)
        entry_texts = entry_content[0].find_all(string=True)
        if len(entry_texts) == 0:
            raise RuntimeError(NO_TEXT_IN_CONTAINER_MSG)
        return "".join(entry_texts).strip()
