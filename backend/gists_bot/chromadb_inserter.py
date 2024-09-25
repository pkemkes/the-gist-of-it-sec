from langchain_core.documents import Document

from gists_utils.chromadb_handler import ChromaDbHandler
from feeds.rss_entry import RSSEntry


REFERENCE_KEY = "reference"


class ChromaDbInserter(ChromaDbHandler):
    def __init__(self) -> None:
        super().__init__()

    def add_or_update_entry(self, entry: RSSEntry) -> None:
        doc = Document(
            page_content=entry.text_content,
            id=entry.reference,
            metadata={REFERENCE_KEY: entry.reference}
        )
        self._chroma_store.add_documents([doc])
