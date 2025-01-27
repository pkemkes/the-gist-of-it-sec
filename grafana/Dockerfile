FROM grafana/grafana-oss:11.4.0

COPY ./grafana/dashboards /var/lib/grafana/dashboards
COPY ./grafana/datasources.yml /etc/grafana/provisioning/datasources/datasources.yml
COPY ./grafana/dashboards.yml /etc/grafana/provisioning/dashboards/dashboards.yml