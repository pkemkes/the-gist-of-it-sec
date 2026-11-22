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

## Development versions

Use Node.js 26.13.1, pnpm 12.21.0, and the .NET 11 SDK. The Python services use Python 3.15.2, except `aiapi`, which uses 3.14.10 because LangSmith's `zstandard` dependency does not yet ship Python 3.15 wheels. TypeScript stays at 6.0.3 until `typescript-eslint` supports TypeScript 7.

Install frontend dependencies with `pnpm install --frozen-lockfile` in `frontend`, then run `pnpm lint` and `pnpm build`. Backend tests run with `dotnet test backend/GistBackend.sln` and require Docker. MCP compatibility tests run with `python -m unittest discover -s mcpserver -v` after installing `mcpserver/requirements.txt`.

## Upgrading existing deployments

The updated stack uses MariaDB 13.0.2 and Grafana 13.3.1. Before deploying, take a database dump and back up the `database-data` and `grafana-data` volumes. `MARIADB_AUTO_UPGRADE=1` lets the official MariaDB entrypoint upgrade existing system tables before the backend starts. For rollback, restore the pre-upgrade backups into fresh volumes and use the previous images; do not attach an upgraded database volume to the older MariaDB image.

Promtail is replaced by Alloy 1.22.0. Update custom deployment files to use the `alloy` service and mount `alloy/config.alloy`. The Loki destination and the `job`, `stream`, and `container_name` labels are preserved. Alloy stores file positions in the new `alloy-data` volume. The old Promtail positions lived in its ephemeral `/tmp` directory, so the initial switch can reread existing container logs; retain Alloy's data volume on subsequent restarts. Grafana's existing provisioned data sources and dashboard definitions are retained, though Grafana's application UI can differ in version 13.

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
