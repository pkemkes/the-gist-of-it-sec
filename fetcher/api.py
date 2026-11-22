from fastapi import FastAPI
from fastapi.concurrency import run_in_threadpool
from contextlib import asynccontextmanager
from requests_handler import RequestsHandler

from typing import Any


handler = RequestsHandler()


@asynccontextmanager
async def lifespan(app: FastAPI):
    try:
        yield
    finally:
        await run_in_threadpool(handler.close)


app = FastAPI(title="Fetch API", version="0.1.0", lifespan=lifespan)


@app.get("/health")
async def health_check() -> dict[str, str]:
    return {"status": "ok"}

@app.get("/fetch")
async def fetch(url: str) -> dict[str, Any]:
    response = await run_in_threadpool(handler.fetch, url)
    return response.model_dump()
