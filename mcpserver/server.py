from typing import Literal

import httpx
from mcp.server.fastmcp import FastMCP
from mcp.types import CallToolResult, TextContent
from pydantic import BaseModel
from starlette.requests import Request
from starlette.responses import JSONResponse, Response

import backend_client
from mcpserver_types import (
    ConstructedGist,
    DeserializedRecap,
    GistsResponse,
    GistsWithSimilarityResponse,
    GistWithSimilarity,
)


def _structured_response(model: BaseModel) -> CallToolResult:
    return CallToolResult(content=[], structuredContent=model.model_dump())


def _error_response(err: Exception) -> CallToolResult:
    if isinstance(err, httpx.HTTPStatusError):
        msg = f"Backend returned {err.response.status_code}: {err.response.text}"
    else:
        msg = f"Backend request failed: {err}"
    return CallToolResult(content=[TextContent(type="text", text=msg)], isError=True)

mcp = FastMCP(
    "The Gist of IT-Sec",
    stateless_http=True,
    json_response=True,
    instructions=(
        "The Gist of IT-Sec aggregates IT security news articles and blog posts "
        "from curated RSS feeds. Each article is automatically summarised by an AI "
        "into a short 'gist'. Gists can be browsed, searched via vector similarity, "
        "or compared to find related coverage. Daily and weekly recaps provide "
        "AI-generated digests that highlight the most important developments across "
        "all collected gists."
    ),
)


@mcp.custom_route("/health", methods=["GET"])
async def health_check(request: Request) -> Response:
    return JSONResponse({"status": "ok"})


@mcp.tool()
async def get_gists(
    take: int = 20,
    last_gist: int | None = None,
    language_mode: Literal["En", "De", "Original"] = "Original",
    include_sponsored_content: bool | None = None,
) -> CallToolResult:
    """Browse the latest AI-summarised IT security news articles and blog posts.

    Returns a paginated list of gists, ordered newest-first. Each gist contains an
    AI-generated summary of the original article, its source feed, tags, and a link
    to the full text.

    Args:
        take: Number of gists to return (default 20).
        last_gist: ID of the last gist from the previous page (for pagination).
        language_mode: Language for the AI summaries: "En" (English), "De" (German), or "Original" (language of the source article).
        include_sponsored_content: Whether to include vendor-sponsored content.
    """
    try:
        result = await backend_client.get_gists(
            take=take,
            last_gist=last_gist,
            language_mode=language_mode,
            include_sponsored_content=include_sponsored_content,
        )
        response = GistsResponse(gists=[ConstructedGist(**g) for g in result])
        return _structured_response(response)
    except (httpx.HTTPStatusError, httpx.RequestError) as e:
        return _error_response(e)


@mcp.tool()
async def search_gists(
    q: str,
    language_mode: Literal["En", "De", "Original"] = "Original",
) -> CallToolResult:
    """Search IT security gists by meaning using vector similarity.

    Performs a semantic search across all AI-generated summaries and returns the
    most relevant gists ranked by cosine similarity. Use natural-language queries
    like "ransomware targeting healthcare" or "zero-day in Chrome".

    Args:
        q: Natural-language search query.
        language_mode: Language for the AI summaries: "En" (English), "De" (German), or "Original" (language of the source article).
    """
    try:
        result = await backend_client.search_gists(
            q=q,
            language_mode=language_mode,
        )
        response = GistsWithSimilarityResponse(gists=[GistWithSimilarity(**g) for g in result])
        return _structured_response(response)
    except (httpx.HTTPStatusError, httpx.RequestError) as e:
        return _error_response(e)


@mcp.tool()
async def get_similar_gists(
    gist_id: int,
    language_mode: Literal["En", "De", "Original"] = "Original",
) -> CallToolResult:
    """Find gists covering similar topics to a given gist.

    Returns gists ranked by cosine similarity to the reference gist's embedding.
    Useful for discovering related coverage or tracking how a story evolved.

    Args:
        gist_id: The numeric ID of the reference gist.
        language_mode: Language for the AI summaries: "En" (English), "De" (German), or "Original" (language of the source article).
    """
    try:
        result = await backend_client.get_similar_gists(
            gist_id, language_mode=language_mode
        )
        response = GistsWithSimilarityResponse(gists=[GistWithSimilarity(**g) for g in result])
        return _structured_response(response)
    except (httpx.HTTPStatusError, httpx.RequestError) as e:
        return _error_response(e)


@mcp.tool()
async def get_daily_recap(language_mode: Literal["En", "De"] = "En") -> CallToolResult:
    """Get the latest daily recap — an AI-generated digest of the day's IT security news.

    The recap is split into thematic sections, each with a heading, summary text,
    and references to the individual gists it draws from.

    Args:
        language_mode: Language for the recap: "En" (English) or "De" (German).
    """
    try:
        result = await backend_client.get_recap("daily", language_mode)
        response = DeserializedRecap(**result)
        return _structured_response(response)
    except (httpx.HTTPStatusError, httpx.RequestError) as e:
        return _error_response(e)


@mcp.tool()
async def get_weekly_recap(language_mode: Literal["En", "De"] = "En") -> CallToolResult:
    """Get the latest weekly recap — an AI-generated digest of the week's IT security news.

    The recap is split into thematic sections, each with a heading, summary text,
    and references to the individual gists it draws from.

    Args:
        language_mode: Language for the recap: "En" (English) or "De" (German).
    """
    try:
        result = await backend_client.get_recap("weekly", language_mode)
        response = DeserializedRecap(**result)
        return _structured_response(response)
    except (httpx.HTTPStatusError, httpx.RequestError) as e:
        return _error_response(e)
