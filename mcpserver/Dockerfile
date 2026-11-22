FROM python:3.15.2-slim

HEALTHCHECK --interval=10s --start-period=10s --retries=3 --timeout=5s \
        CMD curl -f localhost:8000/health || exit 1

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /usr/bin/thegistofitsec

COPY ./requirements.txt requirements.txt
RUN sed -i 's|http://deb.debian.org|https://deb.debian.org|g' /etc/apt/sources.list.d/debian.sources \
    && apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
RUN pip install --no-cache-dir -r requirements.txt

RUN addgroup --gid 1001 mcpserver && \
    yes | adduser --disabled-password --uid 1001 --ingroup mcpserver mcpserver

USER mcpserver

COPY ./ ./

EXPOSE 8000

CMD ["python", "main.py", "--host", "0.0.0.0", "--port", "8000"]
