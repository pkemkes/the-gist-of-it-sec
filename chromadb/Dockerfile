FROM chromadb/chroma:1.4.0

ENV CHROMA_SERVER_AUTHN_PROVIDER=chromadb.auth.token_authn.TokenAuthenticationServerProvider
ENV CHROMA_AUTH_TOKEN_TRANSPORT_HEADER=X-Chroma-Token

HEALTHCHECK --interval=10s --start-period=10s --retries=3 --timeout=5s \
        CMD curl -f http://localhost:8000/api/v2/heartbeat