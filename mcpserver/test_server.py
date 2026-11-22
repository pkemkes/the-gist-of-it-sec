import unittest
from unittest.mock import AsyncMock, patch

import httpx
from starlette.testclient import TestClient

import main
from server import mcp


class MCPCompatibilityTests(unittest.TestCase):
    def setUp(self):
        app = mcp.streamable_http_app(
            host="0.0.0.0", stateless_http=True, json_response=True
        )
        self.client = self.enterContext(TestClient(app))

    def request(self, method, params=None, protocol="2025-11-25"):
        params = dict(params or {})
        if protocol == "2026-07-28":
            params["_meta"] = {
                "io.modelcontextprotocol/protocolVersion": protocol,
                "io.modelcontextprotocol/clientCapabilities": {},
            }
        response = self.client.post(
            "/mcp",
            headers={
                "Accept": "application/json, text/event-stream",
                "MCP-Protocol-Version": protocol,
                "MCP-Method": method,
            },
            json={"jsonrpc": "2.0", "id": 1, "method": method, "params": params},
        )
        self.assertEqual(200, response.status_code, response.text)
        return response.json()["result"]

    def test_health_endpoint(self):
        response = self.client.get("/health")
        self.assertEqual(200, response.status_code)
        self.assertEqual({"status": "ok"}, response.json())

    def test_legacy_client_can_initialize(self):
        result = self.request("initialize", {
            "protocolVersion": "2025-11-25",
            "capabilities": {},
            "clientInfo": {"name": "compatibility-test", "version": "1.0"},
        })
        self.assertEqual("2025-11-25", result["protocolVersion"])
        self.assertEqual("The Gist of IT-Sec", result["serverInfo"]["name"])

    def test_existing_tools_are_available_to_both_protocols(self):
        expected = {
            "get_gists", "search_gists", "get_similar_gists",
            "get_daily_recap", "get_weekly_recap",
        }
        for protocol in ["2025-11-25", "2026-07-28"]:
            with self.subTest(protocol=protocol):
                tools = self.request("tools/list", protocol=protocol)["tools"]
                self.assertEqual(expected, {tool["name"] for tool in tools})

    def test_structured_response_keeps_wire_field_names(self):
        with patch("backend_client.get_gists", new=AsyncMock(return_value=[])):
            result = self.request("tools/call", {"name": "get_gists", "arguments": {}})
        self.assertEqual({"gists": []}, result["structuredContent"])
        self.assertFalse(result.get("isError", False))

    def test_backend_error_remains_a_tool_error(self):
        with patch("backend_client.get_gists", new=AsyncMock(side_effect=httpx.ConnectError("offline"))):
            result = self.request("tools/call", {"name": "get_gists", "arguments": {}})
        self.assertTrue(result["isError"])
        self.assertIn("offline", result["content"][0]["text"])

    def test_cli_preserves_transport_options(self):
        with patch("sys.argv", ["main.py", "--host", "127.0.0.1", "--port", "8765"]), patch.object(mcp, "run") as run:
            main.main()
        run.assert_called_once_with(
            transport="streamable-http", host="127.0.0.1", port=8765,
            stateless_http=True, json_response=True,
        )


if __name__ == "__main__":
    unittest.main()
