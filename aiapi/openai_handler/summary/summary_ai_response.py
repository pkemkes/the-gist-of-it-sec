from pydantic import BaseModel, Field


class SummaryAIResponse(BaseModel):
    summary_english: str = Field(description="The summary of the article in English.")
    summary_german: str = Field(description="The summary of the article in German.")
    title_translated: str = Field(description="The translated title of the article.")
    tags: list[str] = Field(description="A list of tags associated with the article.")
