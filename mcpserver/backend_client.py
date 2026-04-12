import os
import httpx

BACKEND_HOST = os.getenv("BACKEND_HOST", "http://backend:8080")
BACKEND_TIMEOUT = float(os.getenv("BACKEND_TIMEOUT", "30"))

_client = httpx.AsyncClient(base_url=BACKEND_HOST, timeout=BACKEND_TIMEOUT)


async def _get(path: str, params: dict | None = None) -> dict | list:
    response = await _client.get(path, params=params)
    response.raise_for_status()
    return response.json()


async def get_gists(
    take: int = 20,
    last_gist: int | None = None,
    language_mode: str | None = None,
    include_sponsored_content: bool | None = None,
) -> list:
    params: dict = {"take": take}
    if last_gist is not None:
        params["lastGist"] = last_gist
    if language_mode is not None:
        params["languageMode"] = language_mode
    if include_sponsored_content is not None:
        params["includeSponsoredContent"] = str(include_sponsored_content).lower()
    return await _get("/api/v1/gists", params)


async def search_gists(
    q: str,
    language_mode: str | None = None,
) -> list:
    params: dict = {"q": q}
    if language_mode is not None:
        params["languageMode"] = language_mode
    return await _get("/api/v1/gists/search", params)


async def get_similar_gists(
    gist_id: int,
    language_mode: str | None = None,
) -> list:
    params: dict = {}
    if language_mode is not None:
        params["languageMode"] = language_mode
    return await _get(f"/api/v1/gists/{gist_id}/similar", params)


async def get_recap(
    recap_type: str, language_mode: str
) -> dict:
    return await _get(
        f"/api/v1/gists/recap/{recap_type}",
        params={"languageMode": language_mode},
    )
