from gists_utils.chromadb_handler import ChromaDbHandler


class ChromaDbFetcher(ChromaDbHandler):
    def __init__(self) -> None:
        super().__init__()

    @staticmethod
    def validate_disabled_feed_ids(disabled_feed_ids: list[int]) -> bool:
        if type(disabled_feed_ids) != list:
            raise Exception("Disabled feed IDs are not a list!")
        if len(disabled_feed_ids) > 1000:
            raise Exception("Got more than 1000 disabled feed IDs!")
        if not all(
            type(disabled_feed_id) == int and disabled_feed_id >= 0 and disabled_feed_id < 1000000
            for disabled_feed_id in disabled_feed_ids
        ):
            raise Exception("Not all given disabled feed IDs are valid ints!")

    def get_similar_entries_with_relevance_scores(self, reference: str, 
                                                  disabled_feed_ids: list[int]) -> list[tuple[str, float]]:
        self.validate_reference(reference)
        self.validate_disabled_feed_ids(disabled_feed_ids)
        gist_doc = self._collection.get(reference, include=["embeddings"])
        gist_embedding = gist_doc.get("embeddings")
        if gist_embedding is None:
            return []
        filter = { self.disabled_key: { "$ne": True } }
        if len(disabled_feed_ids) > 0:
            filter = { "$and": [ filter, { self.feed_id_key: { "$nin": disabled_feed_ids } } ] }
        search_result = self._chroma_store.similarity_search_by_vector_with_relevance_scores(
            gist_embedding, k=6, filter=filter
        )
        return [(doc.metadata.get(self.reference_key), score) for doc, score in search_result[1:]]
