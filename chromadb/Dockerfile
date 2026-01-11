FROM chromadb/chroma:1.4.0

RUN apt-get update \
        && apt-get install -y --no-install-recommends curl ca-certificates \
        && rm -rf /var/lib/apt/lists/*

ENV CHROMA_SERVER_AUTHN_PROVIDER=chromadb.auth.token_authn.TokenAuthenticationServerProvider
ENV CHROMA_AUTH_TOKEN_TRANSPORT_HEADER=X-Chroma-Token

HEALTHCHECK --interval=10s --start-period=10s --retries=3 --timeout=5s \
        CMD curl -f http://localhost:8000/api/v2/heartbeat