from contextlib import suppress
from typing import Any

from playwright.sync_api import sync_playwright


class PlaywrightClient:
    """Caches a single Chromium context with headers mirrored from curl_cffi defaults."""

    def __init__(self, base_headers: dict[str, str], timeout: float = 20.0):
        self.base_headers = base_headers
        self.timeout_ms = int(timeout * 1000)
        self._playwright = None
        self._browser = None
        self._context = None

    def new_page(self):
        context = self._ensure_context()
        page = context.new_page()
        page.set_default_navigation_timeout(self.timeout_ms)
        page.set_default_timeout(self.timeout_ms)
        return page

    def context_cookies(self) -> list[dict[str, Any]]:
        context = self._ensure_context()
        return context.cookies()

    def _ensure_context(self):
        if self._playwright is None:
            self._playwright = sync_playwright().start()
        if self._browser is None:
            self._browser = self._playwright.chromium.launch(
                headless=True,
                args=[
                    "--disable-blink-features=AutomationControlled",
                    "--disable-dev-shm-usage",
                    "--no-sandbox",
                ],
            )
        if self._context is None:
            self._context = self._browser.new_context(
                user_agent=self.base_headers.get("User-Agent"),
                viewport={"width": 1280, "height": 720},
                locale="en-US",
            )
        return self._context

    def shutdown(self) -> None:
        with suppress(Exception):
            if self._context is not None:
                self._context.close()
        with suppress(Exception):
            if self._browser is not None:
                self._browser.close()
        with suppress(Exception):
            if self._playwright is not None:
                self._playwright.stop()
        self._context = None
        self._browser = None
        self._playwright = None
