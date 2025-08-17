FROM grafana/loki:3.5.3

COPY ./local-config.yaml /etc/loki/local-config.yaml

HEALTHCHECK --interval=30s --start-period=10s --retries=3 --timeout=5s \
        CMD wget --no-verbose --tries=1 --spider http://localhost:3100/ready || exit 1

CMD [ "-config.file=/etc/loki/local-config.yaml" ]
