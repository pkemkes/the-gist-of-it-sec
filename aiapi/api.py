from fastapi import FastAPI
from openai_handler.openai_handler import OpenAIHandler
from models.language import Language
from models.summary_for_recap import SummaryForRecap
from models.recap_type import RecapType
from pydantic import BaseModel
from dotenv import load_dotenv


load_dotenv()

app = FastAPI(title="Summarizer API", version="0.1.0")
handler = OpenAIHandler()


@app.get("/health")
async def health_check() -> dict[str, str]:
    return {"status": "ok"} 

class SummarizeRequest(BaseModel):
    title: str
    article: str
    language: str

@app.post("/summarize")
async def summarize_article(request: SummarizeRequest) -> dict:
    lang_enum = Language(request.language)
    summary_response = await handler.summarize_async(request.title, request.article, lang_enum)
    return summary_response.model_dump()

class RecapRequest(BaseModel):
    summaries: list[SummaryForRecap]
    recap_type: str

@app.post("/recap")
async def recap_article(request: RecapRequest) -> dict:
    recap_type = RecapType(request.recap_type)
    recap_response = await handler.recap_async(request.summaries, recap_type)
    return recap_response.model_dump()
