import time
from http.cookiejar import LWPCookieJar
from pathlib import Path
from pydantic import BaseModel

from curl_cffi import requests


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
        cookie_jar_path: str = "cookies.jar",
    ):
        self.timeout = timeout
        self.retries = retries
        self.backoff_factor = backoff_factor
        self.cookie_jar_path = Path(cookie_jar_path)
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

    def _save_cookies(self) -> None:
        try:
            self.cookie_jar.save(ignore_discard=True, ignore_expires=True)
        except Exception:
            # If saving fails, do not break the flow.
            pass

    def fetch(self, url: str, referrer: str | None = None) -> FetchResponse:
        headers = dict(self.default_headers)
        if referrer:
            headers["Referer"] = referrer
            headers["Sec-Fetch-Site"] = "same-origin"

        last_error: Exception | None = None
        for attempt in range(self.retries):
            try:
                response = self.session.get(url, headers=headers)
                if response.status_code in {500, 502, 503, 504} and attempt < self.retries - 1:
                    delay = self.backoff_factor * (2**attempt)
                    time.sleep(delay)
                    continue
                break
            except requests.RequestsError as exc:
                last_error = exc
                if attempt == self.retries - 1:
                    raise
                delay = self.backoff_factor * (2**attempt)
                time.sleep(delay)
        else:
            raise last_error if last_error else RuntimeError("fetch failed without exception")

        # Persist any first-party cookies set by the origin.
        self._save_cookies()

        # Normalize response text decoding to avoid mangled UTF-8 when servers omit headers.
        encoding = response.encoding or getattr(response, "apparent_encoding", None) or "utf-8"
        response.encoding = encoding
        try:
            content = response.text
        except UnicodeDecodeError:
            content = response.content.decode(encoding, errors="replace")

        return FetchResponse(
            status=response.status_code,
            content=content,
            redirected=str(response.url) != url,
        )