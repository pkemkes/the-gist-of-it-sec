from os import getenv
from urllib.parse import quote
import requests
from requests.adapters import HTTPAdapter, Retry
from typing import Any
import json

from gists_utils.types import Gist, SearchResult
from gists_utils.logger import get_logger
from mariadb_gists_handler import MariaDbGistsHandler


class GoogleSearchHandler:
	def __init__(self, db: MariaDbGistsHandler):
		self._logger = get_logger("google_search_handler")
		self._db = db
		api_key = getenv("GOOGLE_API_KEY")
		cse_id = getenv("GOOGLE_SEARCH_ENGINE_ID")
		self._url = f"https://www.googleapis.com/customsearch/v1?key={api_key}&cx={cse_id}&q="

	def get_value(self, key: str, json_object: dict, search_query: str) -> Any:
		value = json_object.get(key)
		if value is None:
			raise KeyError(f"Did not find {key} in response {json.dumps(json_object)} for search {search_query}")
		return value
	
	def get_search_results(self, gist: Gist) -> list[SearchResult] | None:
		session = requests.Session()
		retry = Retry(total=3, backoff_factor=1, status_forcelist=[ 500, 502, 503, 504 ])
		session.mount("https://", HTTPAdapter(max_retries=retry))
		url = self._url + quote(gist.search_query)
		
		response = requests.get(url)
		if response.status_code == 429:
			self._logger.warning(f"The Google API quota was reached. Got status 429 for search for gist with id {gist.id}")
			return None
		try:
			response_json: dict = response.json()
		except requests.exceptions.JSONDecodeError:
			self._logger.error(
				f"Could not decode search response for gist with id {gist.id}. Response: {response.text}", 
				exc_info=True
			)
			return None
		
		try:
			items = self.get_value("items", response_json, gist.search_query)
			search_results = []
			for item in items:
				pagemap = item.get("pagemap", {})
				search_results.append(SearchResult(
					None,
					gist.id,
					self.get_value("title", item, gist.search_query),
					# Remove date in front of snippet
					"".join(self.get_value("snippet", item, gist.search_query).split("...", maxsplit=1)[1:]).strip(),
					self.get_value("link", item, gist.search_query),
					self.get_value("displayLink", item, gist.search_query),
					pagemap.get("cse_thumbnail", [{}])[0].get("src"),
					pagemap.get("cse_image", [{}])[0].get("src")
				))
		except KeyError:
			self._logger.error(
				f"Key error in search result of search for gist with id {gist.id}",
				exc_info=True
			)
			return None
	
		return search_results
	
	def store_search_results(self, results: list[SearchResult]) -> None:
		for result in results:
			self._db.store_search_result(result)

	def get_and_store_search_results(self, gist: Gist) -> None:
		results = self.get_search_results(gist)
		self._logger.info(
			f"Found {len(results)} search results for gist "
			f"with reference {gist.reference} and query {gist.search_query}"
		)
		self.store_search_results(results)