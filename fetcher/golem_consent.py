import logging
from contextlib import suppress
from typing import Any
from urllib.parse import urlparse

from playwright.sync_api import TimeoutError as PlaywrightTimeoutError
from playwright.sync_api import Page

from playwright_client import PlaywrightClient


logger = logging.getLogger(__name__)


def is_golem_domain(url: str) -> bool:
    host = urlparse(url).netloc.lower()
    return host.endswith("golem.de")


def looks_like_golem_consent(content: str) -> bool:
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

    def get_consent_cookies(self, url: str, referrer: str | None = None) -> tuple[list[dict[str, Any]], str | None]:
        page = self.client.new_page()
        try:
            page.goto(url, wait_until="domcontentloaded", referer=referrer)
            self._click_accept(page)
            with suppress(Exception):
                page.wait_for_load_state("networkidle", timeout=self.timeout_ms)
            page.wait_for_timeout(500)
            cookies = self.client.context_cookies()
            return cookies, page.url
        finally:
            page.close()

    def _click_accept(self, page: Page) -> None:
        # Wait for either the CMP iframe or the GolemConsent API to become available.
        wait_target = """() => document.querySelector("iframe[src*='cmp-cdn.golem.de']") || (window.GolemConsent && typeof window.GolemConsent.acceptAll === 'function')"""
        try:
            page.wait_for_function(wait_target, timeout=self.timeout_ms // 2)
        except PlaywrightTimeoutError:
            logger.warning(f"Golem consent elements not found within timeout for {page.url}")

        # Try inside the CMP iframe first (as seen in response2.html).
        try:
            iframe_el = page.query_selector("iframe[src*='cmp-cdn.golem.de']")
            if iframe_el:
                frame = iframe_el.content_frame()
                if frame:
                    btn = frame.wait_for_selector("button:has-text(\"Zustimmen und weiter\")", timeout=self.timeout_ms // 2)
                    btn.click()
                    return
        except PlaywrightTimeoutError:
            logger.warning(f"Golem consent iframe button not found within timeout for {page.url}")
