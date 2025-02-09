from langchain_core.documents import Document
from prometheus_client import Summary

from gists_utils.chromadb_handler import ChromaDbHandler
from feeds.rss_entry import RSSEntry


UPSERT_CHROMA_SUMMARY = Summary("upsert_chroma_seconds", "Time spent adding documents to chroma")


class ChromaDbInserter(ChromaDbHandler):
    def __init__(self) -> None:
        super().__init__()

    @UPSERT_CHROMA_SUMMARY.time()
    def add_or_update_entry(self, entry: RSSEntry) -> None:
        doc = Document(
            page_content=entry.text_content,
            id=entry.reference,
            metadata={
                self.reference_key: entry.reference,
                self.feed_id_key: entry.feed_id,
                self.disabled_key: False
            }
        )
        self._chroma_store.add_documents([doc])
