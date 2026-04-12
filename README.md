[<img src="./assets/logo.png" alt="screenshot.png" height="100"/>](https://gist.pkemkes.de/) 

[![Version](https://img.shields.io/github/v/release/pkemkes/the-gist-of-it-sec?display_name=tag&label=Version)](https://github.com/pkemkes/the-gist-of-it-sec/releases/latest)
[![Deployment](https://img.shields.io/github/actions/workflow/status/pkemkes/the-gist-of-it-sec/build-and-push.yml?event=push&label=Deployment)](https://github.com/pkemkes/the-gist-of-it-sec/actions/workflows/build-and-push.yml)
[![GitHub license](https://img.shields.io/badge/license-PolyForm%20Noncommercial-blue.svg?label=License)](https://github.com/pkemkes/the-gist-of-it-sec/blob/main/LICENSE.md)

[![Tests](https://img.shields.io/github/actions/workflow/status/pkemkes/the-gist-of-it-sec/test-and-deploy-coverage.yml?branch=main&label=Tests)](https://github.com/pkemkes/the-gist-of-it-sec/actions/workflows/test-and-deploy-coverage.yml)
[![Line Coverage](https://pkemkes.github.io/the-gist-of-it-sec/badge_linecoverage.svg)](https://pkemkes.github.io/the-gist-of-it-sec/)
[![Method Coverage](https://pkemkes.github.io/the-gist-of-it-sec/badge_methodcoverage.svg)](https://pkemkes.github.io/the-gist-of-it-sec/)

[![Tech stack](https://img.shields.io/badge/stack-.NET%20%7C%20Python%20%7C%20React-5c2d91?logo=stackshare&label=Stack)](https://github.com/pkemkes/the-gist-of-it-sec)

----

The Gist of IT Sec aggregates multiple RSS feeds, parses their entries, generates short summaries ("gists") using OpenAIs ChatGPT and collects those in a sleek web UI. A Telegram Bot can also easily be set up that sends a short message to each registered user for every new RSS feed entry.

The web UI furthermore offers the feature to quickly find similar gists using the LLM embeddings.

[<img src="./assets/screenshot.png" alt="screenshot.png" height="500"/>](./assets/screenshot.png)

# Live version

The newest version is always deployed and can freely be used under the following URL: https://gist.pkemkes.de

# Setup

If you want to setup the system yourself, you can modify the [docker-compose.yaml](./docker-compose.yaml) that is used for local development. The production deployment is almost identical (look for "NOTE" remarks in the comments of the file).

The newest docker containers are always automatically built and pushed to https://hub.docker.com/u/pkemkes.

# MCP Server

The project includes an MCP (Model Context Protocol) server that exposes the backend API as tools for AI assistants. Once the stack is running, the MCP server is available at:

```
http(s)://<your-host>/mcp
```

To connect from an MCP-compatible client (e.g. VS Code, Claude Desktop, or Claude Code), add the server URL using Streamable HTTP transport. For example, in Claude Code:

```bash
claude mcp add --transport http the-gist-of-it-sec http(s)://<your-host>/mcp
```

Or in your VS Code `settings.json`:

```json
{
    "mcp": {
        "servers": {
            "the-gist-of-it-sec": {
                "type": "http",
                "url": "http(s)://<your-host>/mcp"
            }
        }
    }
}
```

# License

This project is released under the [PolyForm Noncommercial License 1.0.0](./LICENSE.md). Commercial use is not permitted.

The licenses of other third-party libraries used in this project can be found in [NOTICE](./NOTICE.md).