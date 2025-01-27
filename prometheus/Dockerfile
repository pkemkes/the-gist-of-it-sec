FROM prom/prometheus:v3.1.0

HEALTHCHECK --interval=10s --start-period=10s --retries=3 --timeout=5s \
        CMD wget --no-verbose --tries=1 --spider http://localhost:9090 || exit 1
