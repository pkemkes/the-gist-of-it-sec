docker build -t pkemkes/the-gist-of-it-sec-backend -f .\GistBackend .\backend
docker build -t pkemkes/the-gist-of-it-sec-database .\database
docker build -t pkemkes/the-gist-of-it-sec-chromadb .\chromadb
docker build -t pkemkes/the-gist-of-it-sec-prometheus .\prometheus
docker build -t pkemkes/the-gist-of-it-sec-grafana .\grafana
docker build -t pkemkes/the-gist-of-it-sec-frontend .\frontend