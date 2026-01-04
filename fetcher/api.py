from fastapi import FastAPI
from fastapi.concurrency import run_in_threadpool
from requests_handler import RequestsHandler

from typing import Any


app = FastAPI(title="Fetch API", version="0.1.0")
handler = RequestsHandler()


@app.get("/health")
async def health_check() -> dict[str, str]:
    return {"status": "ok"}

@app.get("/fetch")
async def fetch(url: str) -> dict[str, Any]:
    response = await run_in_threadpool(handler.fetch, url)
    return response.model_dump()
