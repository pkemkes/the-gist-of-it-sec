[<img src="./assets/logo.png" alt="screenshot.png" height="100"/>](https://gist.pkemkes.de/) 

[![GitHub license](https://img.shields.io/badge/license-PolyForm%20Noncommercial-blue.svg)](https://github.com/pkemkes/the-gist-of-it-sec/blob/main/LICENSE)

----

The Gist of IT Sec aggregates multiple RSS feeds, parses their entries, generates short summaries ("gists") using OpenAIs ChatGPT and collects those in a sleek web UI. A Telegram Bot can also easily be set up that sends a short message to each registered user for every new RSS feed entry.

The web UI furthermore offers the feature to quickly find similar gists using the LLM embeddings.

[<img src="./assets/screenshot.png" alt="screenshot.png" height="500"/>](./assets/screenshot.png)

# Live version

The newest version is always deployed and can freely be used under the following URL: https://gist.pkemkes.de

# Setup

If you want to setup the system yourself, you can modify the [docker-compose.yaml](./docker-compose.yaml) that is used for local development. The production deployment is almost identical (look for "NOTE" remarks in the comments of the file).

The newest docker containers are always automatically built and pushed to https://hub.docker.com/u/pkemkes.

# License

This project is released under the [PolyForm Noncommercial License 1.0.0](./LICENSE). Commercial use is not permitted.

The licenses of other third-party libraries used in this project can be found in [NOTICE](./NOTICE.md).