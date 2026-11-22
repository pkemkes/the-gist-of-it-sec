import re
import time
from concurrent.futures import ThreadPoolExecutor
from typing import Any
from urllib.parse import urlparse

from playwright.sync_api import Page

from playwright_client import PlaywrightClient


def is_golem_domain(url: str) -> bool:
    host = (urlparse(url).hostname or "").lower()
    return host == "golem.de" or host.endswith(".golem.de")


def looks_like_golem_consent(content: str) -> bool:
    # Article pages also reference the consent scripts and cookie names.
    if re.search(r'''<article\b[^>]*\bclass\s*=\s*["'][^"']*\bgo-article\b''', content, re.IGNORECASE):
        return False
    lowered = content.lower()
    markers = [
        "golem_consent",
        "willkommen auf golem.de",
        "cookiename\":\"golem_consent20\"",
        "cookies zustimmen",
    ]
    return any(marker in lowered for marker in markers)


class GolemConsentManager:
    def __init__(self, base_headers: dict[str, str], timeout: float = 20.0):
        self.client = PlaywrightClient(base_headers, timeout=timeout)
        self.timeout_ms = int(timeout * 1000)
        # Playwright's synchronous browser/context must stay on their owning thread.
        self._executor = ThreadPoolExecutor(max_workers=1, thread_name_prefix="golem-consent")

    def get_consent_cookies(self, url: str, referrer: str | None = None) -> tuple[list[dict[str, Any]], str | None]:
        return self._executor.submit(self._get_consent_cookies, url, referrer).result()

    def _get_consent_cookies(self, url: str, referrer: str | None) -> tuple[list[dict[str, Any]], str | None]:
        deadline = time.monotonic() + self.timeout_ms / 1000

        def remaining_ms() -> int:
            return max(1, int((deadline - time.monotonic()) * 1000))

        page = self.client.new_page()
        try:
            page.goto(url, wait_until="domcontentloaded", referer=referrer, timeout=remaining_ms())
            if not page.locator("article.go-article").count():
                self._click_accept(page, remaining_ms())
                page.locator("article.go-article").wait_for(state="attached", timeout=remaining_ms())
            cookies = self.client.context_cookies()
            return cookies, page.url
        finally:
            page.close()

    def close(self) -> None:
        try:
            self._executor.submit(self.client.shutdown).result()
        finally:
            self._executor.shutdown()

    def _click_accept(self, page: Page, timeout_ms: int) -> None:
        # The CMP follows the browser locale; en-US uses "Accept and Continue".
        label = re.compile(r"^(Zustimmen und weiter|Accept and Continue)$", re.IGNORECASE)
        page.frame_locator('iframe[src*="cmp-cdn.golem.de"]').get_by_role(
            "button", name=label
        ).click(timeout=timeout_ms)
