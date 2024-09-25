docker build -t pkemkes/the-gist-of-it-sec-gists-bot -f ./backend/Dockerfile.gists_bot ./backend
docker build -t pkemkes/the-gist-of-it-sec-telegram-bot -f ./backend/Dockerfile.telegram_bot ./backend
docker build -t pkemkes/the-gist-of-it-sec-rest-server -f ./backend/Dockerfile.rest_server ./backend
docker build -t pkemkes/the-gist-of-it-sec-database ./database
docker build -t pkemkes/the-gist-of-it-sec-chromadb ./chromadb
docker build -t pkemkes/the-gist-of-it-sec-frontend ./frontend