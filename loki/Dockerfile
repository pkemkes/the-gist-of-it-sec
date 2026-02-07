FROM grafana/loki:3.6.5

COPY ./local-config.yaml /etc/loki/local-config.yaml

HEALTHCHECK --interval=10s --start-period=60s --retries=6 --timeout=5s \
        CMD [ "loki", "-health" ]

CMD [ "-config.file=/etc/loki/local-config.yaml" ]
