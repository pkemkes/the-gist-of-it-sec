from langchain_core.documents import Document

from gists_utils.chromadb_handler import ChromaDbHandler
from feeds.rss_entry import RSSEntry


class ChromaDbInserter(ChromaDbHandler):
    def __init__(self) -> None:
        super().__init__()

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
