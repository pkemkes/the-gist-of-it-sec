FROM chromadb/chroma:0.5.23

HEALTHCHECK --interval=10s --start-period=10s --retries=3 --timeout=5s \
        CMD curl -f http://localhost:8000/api/v2/heartbeat