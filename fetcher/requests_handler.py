import logging
import os
import time
from http.cookiejar import Cookie, LWPCookieJar
from pathlib import Path
from typing import Any

from curl_cffi import requests
from pydantic import BaseModel

from golem_consent import GolemConsentManager, is_golem_domain, looks_like_golem_consent


logger = logging.getLogger(__name__)


class FetchResponse(BaseModel):
	status: int
	content: str
	redirected: bool


class RequestsHandler:
    def __init__(
        self,
        timeout: float = 20.0,
        retries: int = 3,
        backoff_factor: float = 0.5,
        cookie_jar_path: str | Path | None = None,
    ):
        self.timeout = timeout
        self.retries = retries
        self.backoff_factor = backoff_factor
        configured_path = cookie_jar_path or os.getenv("COOKIE_JAR_PATH", "/var/lib/fetcher/cookies.jar")
        self.cookie_jar_path = Path(configured_path)
        self.cookie_jar_path.parent.mkdir(parents=True, exist_ok=True)
        self.cookie_jar = LWPCookieJar(str(self.cookie_jar_path))
        if self.cookie_jar_path.exists():
            try:
                self.cookie_jar.load(ignore_discard=True, ignore_expires=True)
            except Exception:
                # If the jar is corrupted, start fresh.
                self.cookie_jar = LWPCookieJar(str(self.cookie_jar_path))

        self.default_headers = {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
            "Accept-Language": "en-US,en;q=0.9",
            "Accept-Encoding": "gzip, deflate, br",
            "Cache-Control": "no-cache",
            "Pragma": "no-cache",
            "Connection": "keep-alive",
            "Upgrade-Insecure-Requests": "1",
            "Sec-Fetch-Dest": "document",
            "Sec-Fetch-Mode": "navigate",
            "Sec-Fetch-Site": "none",
            "Sec-Fetch-User": "?1",
            "sec-ch-ua": '"Not_A Brand";v="99", "Chromium";v="131", "Google Chrome";v="131"',
            "sec-ch-ua-mobile": "?0",
            "sec-ch-ua-platform": '"Windows"',
        }
        # curl_cffi expects http_version instead of the old http2 flag
        self.session = requests.Session(impersonate="chrome", http_version=2, timeout=self.timeout)
        self.session.headers.update(self.default_headers)
        self.session.cookies = self.cookie_jar
        self._golem_consent_mgr = GolemConsentManager(self.default_headers, timeout=self.timeout)

    def _save_cookies(self) -> None:
        try:
            self.cookie_jar.save(ignore_discard=True, ignore_expires=True)
        except Exception:
            # If saving fails, do not break the flow.
            logger.warning(f"Failed to save cookies to {self.cookie_jar_path}; continuing without persistence", exc_info=True)

    def fetch(self, url: str, referrer: str | None = None) -> FetchResponse:
        headers = dict(self.default_headers)
        if referrer:
            headers["Referer"] = referrer
            headers["Sec-Fetch-Site"] = "same-origin"

        tried_golem_consent = False
        current_url = url

        while True:
            response = self._perform_request(current_url, headers)

            # Persist any first-party cookies set by the origin.
            self._save_cookies()

            encoding = response.encoding or getattr(response, "apparent_encoding", None) or "utf-8"
            response.encoding = encoding
            try:
                content = response.text
            except UnicodeDecodeError:
                content = response.content.decode(encoding, errors="replace")

            if self._should_attempt_golem_consent(current_url, response.status_code, content) and not tried_golem_consent:
                tried_golem_consent = True
                if self._solve_golem_consent(current_url, referrer):
                    # After setting cookies via Playwright, retry the request.
                    continue

            return FetchResponse(
                status=response.status_code,
                content=content,
                redirected=str(response.url) != current_url,
            )

    def _perform_request(self, url: str, headers: dict[str, str]):
        last_error: Exception | None = None
        for attempt in range(self.retries):
            try:
                response = self.session.get(url, headers=headers)
                if response.status_code in {500, 502, 503, 504} and attempt < self.retries - 1:
                    delay = self.backoff_factor * (2**attempt)
                    time.sleep(delay)
                    continue
                return response
            except requests.RequestsError as exc:
                last_error = exc
                if attempt == self.retries - 1:
                    raise
                delay = self.backoff_factor * (2**attempt)
                time.sleep(delay)
        raise last_error if last_error else RuntimeError("fetch failed without exception")

    def _solve_golem_consent(self, url: str, referrer: str | None) -> bool:
        try:
            cookies, _ = self._golem_consent_mgr.get_consent_cookies(url, referrer)
        except Exception:
            return False

        if not cookies:
            return False

        self._store_playwright_cookies(cookies)
        self._save_cookies()
        return True

    def _store_playwright_cookies(self, cookies: list[dict[str, Any]]) -> None:
        for cookie in cookies:
            domain = cookie.get("domain", "")
            domain_initial_dot = domain.startswith(".")
            c = Cookie(
                version=0,
                name=cookie.get("name", ""),
                value=cookie.get("value", ""),
                port=None,
                port_specified=False,
                domain=domain.lstrip("."),
                domain_specified=bool(domain),
                domain_initial_dot=domain_initial_dot,
                path=cookie.get("path", "/"),
                path_specified=True,
                secure=cookie.get("secure", False),
                expires=int(cookie.get("expires")) if cookie.get("expires") else None,
                discard=False,
                comment=None,
                comment_url=None,
                rest={"HttpOnly": cookie.get("httpOnly", False)},
                rfc2109=False,
            )
            self.cookie_jar.set_cookie(c)

    def _should_attempt_golem_consent(self, url: str, status_code: int, content: str) -> bool:
        if not is_golem_domain(url):
            return False
        if status_code in {401, 402, 403}:
            return True
        return looks_like_golem_consent(content)