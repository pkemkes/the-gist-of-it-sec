from gists_utils.chromadb_handler import ChromaDbHandler


REFERENCE_KEY = "reference"


class ChromaDbFetcher(ChromaDbHandler):
    def __init__(self) -> None:
        super().__init__()

    def get_similar_entries_with_relevance_scores(self, reference: str, k: int) -> list[tuple[str, float]]:
        doc = self._chroma_store.get(reference, include=["embeddings"])
        embedding = doc.get("embeddings")
        search_result = self._chroma_store.similarity_search_by_vector_with_relevance_scores(embedding, k=k)
        return [(doc.metadata.get(REFERENCE_KEY), score) for doc, score in search_result[1:]]
