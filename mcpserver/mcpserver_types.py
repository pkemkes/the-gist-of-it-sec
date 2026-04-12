from pydantic import BaseModel, Field


class ConstructedGist(BaseModel):
    id: int
    reference: str = Field(description="Unique reference key for this gist.")
    feedTitle: str = Field(description="Name of the RSS feed this article came from.")
    feedUrl: str = Field(description="URL of the source RSS feed.")
    feedType: int = Field(description="Source type: 0 = News outlet, 1 = Blog.")
    title: str = Field(description="Title of the original article.")
    author: str
    isSponsoredContent: bool = Field(description="Whether this article is vendor-sponsored content.")
    url: str = Field(description="URL of the original full-text article.")
    published: str = Field(description="ISO 8601 publication timestamp.")
    updated: str = Field(description="ISO 8601 last-updated timestamp.")
    summary: str = Field(description="AI-generated summary of the article.")
    tags: list[str] = Field(description="AI-assigned topic tags (e.g. 'ransomware', 'vulnerability').")


class GistWithSimilarity(BaseModel):
    gist: ConstructedGist
    similarity: float = Field(description="Cosine similarity score (0.0–1.0). Higher means more relevant.")


class RelatedGistInfo(BaseModel):
    id: int
    title: str


class DeserializedRecapSection(BaseModel):
    heading: str = Field(description="Thematic heading for this section of the recap.")
    recap: str = Field(description="AI-generated summary text for this section.")
    related: list[RelatedGistInfo] = Field(description="Gists that this section draws from.")


class DeserializedRecap(BaseModel):
    created: str = Field(description="ISO 8601 timestamp when this recap was generated.")
    recapSections: list[DeserializedRecapSection]
    id: int


class GistsResponse(BaseModel):
    gists: list[ConstructedGist]


class GistsWithSimilarityResponse(BaseModel):
    gists: list[GistWithSimilarity]
