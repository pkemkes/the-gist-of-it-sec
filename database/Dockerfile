FROM mariadb:12.1.2

HEALTHCHECK --interval=10s --start-period=10s --retries=3 --timeout=5s \
        CMD healthcheck.sh --connect --innodb_initialized

COPY ./ /docker-entrypoint-initdb.d/
RUN chmod 644 /docker-entrypoint-initdb.d/*