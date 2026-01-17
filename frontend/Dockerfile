FROM node:25.3.0-alpine3.23 AS build

ARG NODE_ENV=production
ENV NODE_ENV=${NODE_ENV}

ARG VITE_APP_VERSION=0.0.0
ENV VITE_APP_VERSION=${VITE_APP_VERSION}

ENV VITE_BACKEND_URL=http://localhost:8080/gists

WORKDIR /app

RUN npm install -g pnpm http-server

COPY . .

RUN pnpm install

CMD pnpm run build && http-server /app/build -p 80
