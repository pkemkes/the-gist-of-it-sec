## Plan: Set Up Promtail on Windows for Docker Logs to Loki

1. Download the latest Promtail binary for Windows from the official Grafana Loki releases.
2. Create a Promtail configuration file (`promtail-config.yaml`) to scrape Docker container log files.
3. Identify the location of Docker container log files (usually under `C:\ProgramData\Docker\containers\<container-id>\<container-id>-json.log`).
4. Adjust the Promtail config to match your log file paths and set appropriate labels (e.g., container name, service).
5. Start Promtail as a background process or Windows service, pointing to your config file.
6. Verify Promtail is sending logs to Loki by checking the Loki UI or querying the API.
7. Update Grafana queries to use the labels set by Promtail for log filtering.
