import json
from typing import List
from os import getenv
from datetime import datetime, timedelta, timezone

from langchain.agents import create_agent
from langchain_openai.chat_models import ChatOpenAI
from langchain_core.messages import SystemMessage, HumanMessage
from langchain_core.prompts import SystemMessagePromptTemplate, HumanMessagePromptTemplate

from models.language import Language
from openai_handler.summary.summary_ai_response import SummaryAIResponse

from models.recap_type import RecapType
from models.summary_for_recap import SummaryForRecap
from openai_handler.recap.recap_ai_response import RecapAIResponse

class OpenAIHandler:
    def __init__(self):
        self.tags = self._load_tags()
        self.summary_user_message_template = self._load_summary_user_message_template()
        self.model = ChatOpenAI(model=getenv("OPENAI_MODEL", "gpt-5-mini"))
        self.summary_agent = create_agent(
            model=self.model,
            response_format=SummaryAIResponse,
            system_prompt=self._load_summary_system_message(),
        )
        self.recap_system_message_template = self._load_recap_system_message_template()
        self.recap_user_message_template = self._load_recap_user_message_template()


    def _load_tags(self) -> List[str]:
        with open("openai_handler/summary/tags.json") as f:
            return [tag.lower().strip() for tag in json.load(f)]
        
    def _load_summary_system_message(self) -> SystemMessage:
        with open("openai_handler/summary/system.txt") as f:
            return SystemMessagePromptTemplate.from_template(f.read()).format(
                tags=self.tags
            )
    
    def _load_summary_user_message_template(self) -> str:
        with open("openai_handler/summary/user.txt") as f:
            return f.read()

    def _get_summary_user_message(self, language: Language, title: str, article: str) -> HumanMessage:
        return HumanMessagePromptTemplate.from_template(self.summary_user_message_template).format(
            original_language=language.value,
            translation_language=language.invert().value,
            title=title,
            article=article
        )
    
    def _filter_tags(self, generated_tags: List[str]) -> List[str]:
        return [
            tag for tag in generated_tags 
            if type(tag) is str and tag.lower().strip() in self.tags
        ]
    
    async def summarize_async(self, title: str, article: str, language: Language) -> SummaryAIResponse:
        result: dict = await self.summary_agent.ainvoke({
            "messages": [ self._get_summary_user_message(language, title, article) ]
        })
        response = result.get("structured_response")
        if response is None:
            raise ValueError("No structured response from summary agent")
        response.tags = self._filter_tags(response.tags)
        return response
    
    def _load_recap_system_message(self) -> SystemMessage:
        with open("openai_handler/recap/system.txt") as f:
            return SystemMessagePromptTemplate.from_template(f.read()).format()
        
    def _load_recap_system_message_template(self) -> str:
        with open("openai_handler/recap/system.txt") as f:
            return f.read()

    def _load_recap_user_message_template(self) -> str:
        with open("openai_handler/recap/user.txt") as f:
            return f.read()
        
    def _get_recap_system_prompt(self, recap_type: RecapType) -> SystemMessage:
        timeframe_desc = "24 hours" if recap_type == RecapType.Daily else "7 days"
        from_time = (datetime.now(timezone.utc) - (
            timedelta(days=1 if recap_type == RecapType.Daily else 7)
        )).isoformat()
        to_time = datetime.now(timezone.utc).isoformat()
        return SystemMessagePromptTemplate.from_template(
            self.recap_system_message_template
        ).format(
            timeframe_desc=timeframe_desc,
            from_time=from_time,
            to_time=to_time
        )
    
    def _get_recap_agent(self, recap_type: RecapType):
        return create_agent(
            model=self.model,
            response_format=RecapAIResponse,
            system_prompt=self._get_recap_system_prompt(recap_type),
        )

    def _get_recap_user_message(self, summary: SummaryForRecap) -> HumanMessage:
        return HumanMessagePromptTemplate.from_template(
            self.recap_user_message_template
        ).format(
            title=summary.title,
            summary=summary.summary,
            id=summary.id
        )
    
    async def recap_async(self, summaries: list[SummaryForRecap], recap_type: RecapType) -> RecapAIResponse:
        agent = self._get_recap_agent(recap_type)
        result: dict = await agent.ainvoke({
            "messages": [ self._get_recap_user_message(summary) for summary in summaries ]
        })
        response = result.get("structured_response")
        if response is None:
            raise ValueError("No structured response from recap agent")
        return response
