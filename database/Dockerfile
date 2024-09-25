FROM mariadb:11.6.2

HEALTHCHECK --interval=10s --start-period=10s --retries=3 --timeout=5s \
        CMD healthcheck.sh --connect --innodb_initialized

COPY ./ /docker-entrypoint-initdb.d/