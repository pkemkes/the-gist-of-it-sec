from pydantic import BaseModel


class SummaryForRecap(BaseModel):
    title: str
    summary: str
    id: int
