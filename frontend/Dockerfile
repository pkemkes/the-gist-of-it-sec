FROM node:26.13.1-alpine3.24 AS build

ARG NODE_ENV=production
ENV NODE_ENV=${NODE_ENV}

ARG VITE_APP_VERSION=0.0.0
ENV VITE_APP_VERSION=${VITE_APP_VERSION}

ENV VITE_BACKEND_URL=http://localhost:8080/gists

WORKDIR /app

RUN npm install -g pnpm@12.21.0 http-server@14.1.1

COPY . .

RUN pnpm install --frozen-lockfile --prod=false

CMD pnpm run build && http-server /app/build -p 80
