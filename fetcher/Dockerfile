FROM python:3.14.3-slim

HEALTHCHECK --interval=10s --start-period=10s --retries=3 --timeout=5s \
        CMD curl localhost:8000/health || exit 1

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    COOKIE_JAR_PATH=/var/lib/fetcher/cookies.jar

WORKDIR /usr/bin/thegistofitsec

COPY ./requirements.txt requirements.txt
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
RUN pip install --no-cache-dir -r requirements.txt
RUN addgroup --gid 1001 fetcher && \
    yes | adduser --disabled-password --uid 1001 --ingroup fetcher fetcher

# Dedicated writable location for cookies only
RUN mkdir -p /var/lib/fetcher && chown fetcher:fetcher /var/lib/fetcher

ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN python -m playwright install --with-deps chromium
RUN chown -R fetcher:fetcher /ms-playwright

COPY ./ ./

USER fetcher

EXPOSE 8000

CMD ["python", "main.py", "--host", "0.0.0.0", "--port", "8000"]
