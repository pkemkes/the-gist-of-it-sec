from os import getenv

from langchain_openai import OpenAIEmbeddings
from langchain_chroma.vectorstores import Chroma
import chromadb
from chromadb.config import Settings as ChromaDbSettings


class ChromaDbHandler:
	def __init__(self) -> None:
		self.reference_key = "reference"
		self.feed_id_key = "feed_id"
		self.disabled_key = "disabled"

		self._embedding_function = OpenAIEmbeddings() if getenv("OPENAI_API_KEY") else None
		self._chroma_client = chromadb.HttpClient(
			host=getenv("CHROMA_HOST"),
			settings=ChromaDbSettings(
				chroma_auth_token_transport_header=getenv("CHROMA_AUTH_TOKEN_TRANSPORT_HEADER"),
				chroma_client_auth_provider="chromadb.auth.token_authn.TokenAuthClientProvider",
				chroma_client_auth_credentials=getenv("CHROMA_SERVER_AUTHN_CREDENTIALS")
			)
		)
		collection_name = "gist_text_contents"
		self._collection = self._chroma_client.get_or_create_collection(collection_name)
		self._chroma_store = Chroma(
			client=self._chroma_client,
			collection_name=collection_name,
			embedding_function=self._embedding_function
		)
	
	@staticmethod
	def validate_reference(reference: str):
		if type(reference) != str:
			raise Exception("Reference is not a str!")
		if len(reference) == 0 or len(reference) > 1000000:
			raise Exception(f"Reference length is invalid: {len(reference)}")
