FROM prom/prometheus:v3.16.0

COPY ./prometheus.yml /etc/prometheus/prometheus.yml

HEALTHCHECK --interval=10s --start-period=10s --retries=3 --timeout=5s \
        CMD wget --no-verbose --tries=1 --spider http://localhost:9090 || exit 1
