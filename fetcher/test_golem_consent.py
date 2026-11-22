import tempfile
import threading
import unittest
from concurrent.futures import ThreadPoolExecutor
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from unittest.mock import patch

from golem_consent import GolemConsentManager
from requests_handler import RequestsHandler


class ConsentFixture(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_GET(self):
        if self.path == "/article":
            if "golem_consent20=accepted" not in self.headers.get("Cookie", ""):
                self.send_response(302)
                self.send_header("Location", "/sonstiges/zustimmung/auswahl.html")
                self.end_headers()
                return
            body = '<article class="go-article"><p>Public article text</p></article><!-- golem_consent20 -->'
        elif self.path == "/cmp-cdn.golem.de/index.html":
            body = """<button aria-label="Accept and Continue" onclick="
                parent.document.cookie='golem_consent20=accepted; path=/';
                parent.location.href='/article';">Accept and Continue</button>"""
        else:
            body = """<h1>Willkommen auf Golem.de</h1>
                <iframe src="/cmp-cdn.golem.de/index.html"></iframe>"""
        data = body.encode()
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


class GolemConsentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = ThreadingHTTPServer(("127.0.0.1", 0), ConsentFixture)
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()
        cls.article_url = f"http://127.0.0.1:{cls.server.server_port}/article"

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join()

    def test_english_consent_and_session_cookie_survive_fetcher_restart(self):
        with tempfile.TemporaryDirectory() as directory, patch("requests_handler.is_golem_domain", return_value=True):
            cookie_path = Path(directory) / "cookies.jar"
            handler = RequestsHandler(timeout=10, retries=1, cookie_jar_path=cookie_path)
            try:
                result = handler.fetch(self.article_url)
                self.assertEqual(200, result.status)
                self.assertIn("Public article text", result.content)
                cookie = next(c for c in handler.cookie_jar if c.name == "golem_consent20")
                self.assertIsNone(cookie.expires)
            finally:
                handler.close()

            handler = RequestsHandler(timeout=10, retries=1, cookie_jar_path=cookie_path)
            try:
                with patch.object(handler._golem_consent_mgr, "get_consent_cookies") as consent:
                    result = handler.fetch(self.article_url)
                    self.assertIn("Public article text", result.content)
                    consent.assert_not_called()
            finally:
                handler.close()

    def test_cached_browser_is_safe_across_different_request_workers(self):
        manager = GolemConsentManager({}, timeout=10)
        try:
            for _ in range(2):
                with ThreadPoolExecutor(max_workers=1) as worker:
                    cookies, final_url = worker.submit(
                        manager.get_consent_cookies, self.article_url
                    ).result(timeout=20)
                self.assertEqual(self.article_url, final_url)
                self.assertTrue(any(c["name"] == "golem_consent20" for c in cookies))
        finally:
            manager.close()


if __name__ == "__main__":
    unittest.main()
