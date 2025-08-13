
1. Add Promtail as a service in `docker-compose.yaml` (Linux only).
   - Mount Docker log directory or Docker socket as a volume.
   - Mount a `promtail-config.yaml` file as a volume.
2. Create `promtail-config.yaml` to scrape backend container logs and push to Loki (`http://loki:3100/loki/api/v1/push`).
   - Use Linux paths for log files or Docker socket.
   - Set labels (job, container name).
3. Ensure Promtail has required permissions to access Docker logs or socket.
4. On Windows, do not run Promtail. Docker logs remain accessible via `docker logs` command.
5. Verify logs from backend appear in Loki and Grafana on Linux. Troubleshoot if needed.
