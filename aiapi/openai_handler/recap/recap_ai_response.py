from pydantic import BaseModel, Field


class RecapSection(BaseModel):
    heading: str = Field(description="The heading of the recap section")
    recap: str = Field(description="The recap content for the section")
    related: list[int] = Field(description="List of related gist IDs")


class RecapAIResponse(BaseModel):
    recap_sections_english: list[RecapSection] = Field(
        description="List of recap sections in English"
    )
    recap_sections_german: list[RecapSection] = Field(
        description="List of recap sections in German"
    )
