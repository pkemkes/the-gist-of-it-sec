FROM python:3.12-slim

WORKDIR /usr/bin/thegistofitsec

COPY ./requirements.txt requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

RUN addgroup --gid 1001 feed_watcher && \
    yes | adduser --disabled-password --uid 1001 --ingroup feed_watcher feed_watcher

USER feed_watcher

COPY ./ ./

CMD ["python", "main.py"]
