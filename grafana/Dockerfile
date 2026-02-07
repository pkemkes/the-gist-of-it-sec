FROM grafana/grafana-oss:12.3.2

COPY ./dashboards /var/lib/grafana/dashboards
COPY ./datasources.yml /etc/grafana/provisioning/datasources/datasources.yml
COPY ./dashboards.yml /etc/grafana/provisioning/dashboards/dashboards.yml