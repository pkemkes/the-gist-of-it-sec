from api import app
from flask import Response, request, jsonify

from gists_utils.types import Gist, FeedInfo
from api_data import (
	GistApiResponse, 
	FeedInfoApiResponse, 
	SimilarGistApiResponse,
)
from mariadb_rest_handler import MariaDbRestHandler
from chromadb_fetcher import ChromaDbFetcher


DB = MariaDbRestHandler()
CHROMA = ChromaDbFetcher()


def gist_to_api_data(gist: Gist) -> GistApiResponse:
    feed_info = DB.get_feed_by_id(gist.feed_id)
    return GistApiResponse(
        gist.id,
        feed_info.title,
        feed_info.link,
        gist.author,
        gist.title,
        gist.published.isoformat(),
        gist.updated.isoformat(),
        gist.link,
        gist.summary,
        gist.tags,
        gist.search_query
    )


def gist_and_similarity_to_api_data(gist: Gist, similarity: float) -> SimilarGistApiResponse:
    return SimilarGistApiResponse(
        gist_to_api_data(gist),
        similarity
    )


@app.route("/gists", methods=["GET"])
def get_gists() -> Response:
    last_gist_id = int(request.args.get("last_gist", "-1"))
    take = int(request.args.get("take", "20"))
    search_query = request.args.get("q")
    tags = [tag for tag in request.args.get("tags", "").split(";;") if tag != ""]
    disabled_feeds = [int(df) for df in request.args.get("disabled_feeds", "").split(",") if df != ""]
    gists = DB.get_prev_gists(last_gist_id, take, search_query, tags, disabled_feeds)
    response_data = [gist_to_api_data(g) for g in gists]
    return jsonify(response_data), 200


def get_gist_id_from_request() -> tuple[int | None, Response | None]:
    gist_id_param = request.args.get("id")
    if not gist_id_param:
        return None, ("Parameter \"id\" is missing", 400)
    if not gist_id_param.isdigit():
        return None, ("Parameter \"id\" is not a valid number", 400)
    return int(gist_id_param), None


def get_gist_by_id_in_request() -> tuple[Gist | None, Response | None]:
    gist_id, error = get_gist_id_from_request()
    if error is not None:
        return None, error
    gist = DB.get_gist_by_id(gist_id)
    if gist is None:
        return None, ("Could not find gist with given id", 404)
    return gist, None


@app.route("/gists/by_id", methods=["GET"])
def get_gist_by_id() -> Response:
    gist, error = get_gist_by_id_in_request()
    if error is not None:
        return error
    return jsonify(gist_to_api_data(gist)), 200


@app.route("/gists/similar", methods=["GET"])
def get_similar_gists() -> Response:
    gist, error = get_gist_by_id_in_request()
    if error is not None:
        return error
    # ToDo: This is pretty hacky. We are using k=20 and only take 5 gists. 
    # 1 is always the given gist, so 19 potential similar gists. 
    # If more than 14 gists are disabled, we won't be able to return 5 similar gists.
    search_result = CHROMA.get_similar_entries_with_relevance_scores(gist.reference, k=20)
    gists_and_scores = [(DB.get_gist_by_reference(reference), score) for reference, score in search_result]
    gists_and_scores = [(gist, score) for gist, score in gists_and_scores if gist is not None][:5]
    response_data = [
        gist_and_similarity_to_api_data(gist, score) 
        for gist, score in gists_and_scores
    ]
    return jsonify(response_data), 200


@app.route("/gists/search_results", methods=["GET"])
def get_search_results() -> Response:
    gist_id, error = get_gist_id_from_request()
    if error is not None:
        return error
    results = DB.get_search_results_by_gist_id(gist_id)
    return jsonify(results), 200


def feed_info_to_api_data(feed_info: FeedInfo) -> FeedInfoApiResponse:
    return FeedInfoApiResponse(
        feed_info.id,
        feed_info.title,
        feed_info.link,
        feed_info.language
    )


@app.route("/feeds", methods=["GET"])
def get_feeds() -> Response:
    all_feed_info = DB.get_all_feed_info()
    response_data = [feed_info_to_api_data(fi) for fi in all_feed_info]
    return jsonify(response_data), 200
