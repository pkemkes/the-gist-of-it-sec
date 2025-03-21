from api import app
from flask import Response, request, jsonify

from gists_utils.types import Gist, FeedInfo
from api_data import (
	GistApiResponse, 
	FeedInfoApiResponse, 
	SimilarGistApiResponse,
    RecapApiResponse,
    RecapCategoryApiResponse,
    RelatedGistInRecap,
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


def get_disabled_feeds_from_request() -> tuple[list[int] | None, Response | None]:
    disabled_feeds_param = request.args.get("disabled_feeds")
    if disabled_feeds_param is None:
        return [], None
    split_disabled_feeds_param = [df for df in disabled_feeds_param.split(",") if df != ""]
    if not all(disabled_feed_str.isdigit() for disabled_feed_str in split_disabled_feeds_param):
        return None, ("Parameter \"disabled_feeds\" contains elements that are not valid numbers!", 400)
    return [int(df) for df in split_disabled_feeds_param], None


def get_gists(last_gist_id: int, take: int, search_query: str, tags: list[str], disabled_feeds: list[int]):
    gists = DB.get_prev_gists(last_gist_id, take, search_query, tags, disabled_feeds)
    return [gist_to_api_data(g) for g in gists]


@app.route("/gists", methods=["GET"])
def get_gists_route() -> Response:
    last_gist_id = int(request.args.get("last_gist", "-1"))
    take = int(request.args.get("take", "20"))
    search_query = request.args.get("q")
    tags = [tag for tag in request.args.get("tags", "").split(";;") if tag != ""]
    disabled_feeds, error = get_disabled_feeds_from_request()
    if error is not None:
        return error
    response_data = get_gists(last_gist_id, take, search_query, tags, disabled_feeds)
    return jsonify(response_data), 200


@app.route("/health", methods=["GET"])
def health_route() -> Response:
    response_data = get_gists(-1, 1, None, [], [])
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
    disabled_feeds, error = get_disabled_feeds_from_request()
    if error is not None:
        return error
    search_result = CHROMA.get_similar_entries_with_relevance_scores(gist.reference, disabled_feed_ids=disabled_feeds)
    gists_and_scores = [(DB.get_gist_by_reference(reference), score) for reference, score in search_result]
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


@app.route("/recap", methods=["GET"])
def get_recap() -> Response:
    recap_type = request.args.get("type")
    if recap_type == "daily":
        result = DB.get_daily_recap()
    elif recap_type == "weekly":
        result = DB.get_weekly_recap()
    else:
        return "Given value for parameter type is invalid!", 400
    if result is None:
        return "Could not retrieve latest recap", 500
    categories, created = result
    gist_ids = sorted(set(sum([cat.related for cat in categories], [])))
    gist_titles = DB.get_gist_titles_for_ids(gist_ids)
    gist_titles_by_ids = dict(zip(gist_ids, gist_titles))
    categories_api_response = [ 
        RecapCategoryApiResponse(
            cat.heading, 
            cat.recap,
            [ 
                RelatedGistInRecap(gist_id, gist_titles_by_ids[gist_id]) 
                for gist_id in cat.related 
            ]
        ) for cat in categories ]
    recap = RecapApiResponse(
        categories_api_response,
        created.isoformat()
    )
    return jsonify(recap), 200
