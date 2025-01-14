from gists_utils.chromadb_handler import ChromaDbHandler
from gists_utils.types import Gist


class ChromaDbCleanupHandler(ChromaDbHandler):
    def __init__(self) -> None:
        super().__init__()

    def _set_disabled(self, reference: str, disabled: bool) -> None:
        old_metadatas = self._collection.get(reference, include=["metadatas"]).get("metadatas")
        if old_metadatas is None or len(old_metadatas) == 0:
            return
        new_metadata = old_metadatas[0]
        new_metadata[self.disabled_key] = disabled
        self._collection.update(reference, metadatas=new_metadata)
    
    def disable_gist(self, gist: Gist):
        self._set_disabled(gist.reference, True)
    
    def enable_gist(self, gist: Gist):
        self._set_disabled(gist.reference, False)
    
    def get_metadata(self, reference: str) -> dict:
        metadatas = self._collection.get(reference, include=["metadatas"]).get("metadatas")
        if metadatas is None or len(metadatas) == 0:
            return {}
        return metadatas[0]

    def set_metadata(self, reference: str, metadata: dict) -> None:
        self._collection.update(reference, metadatas=metadata)
