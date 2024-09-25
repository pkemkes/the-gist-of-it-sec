import json
from typing import Any, List
from datetime import datetime

from langchain_openai import ChatOpenAI
from langchain_core.messages import SystemMessage
from langchain_core.prompts import HumanMessagePromptTemplate, ChatPromptTemplate
from langchain_core.output_parsers import JsonOutputParser
from langchain_core.runnables import RunnableSerializable

from feeds.rss_entry import RSSEntry
from gists_utils.logger import get_logger
from gists_utils.types import AIResponse


class OpenAIHandler:
    def __init__(self) -> None:
        self.tags = self._load_tags()
        self.tags_lower = [tag.lower().strip() for tag in self.tags]
        self.chat_model = ChatOpenAI(
            model="gpt-4o-mini"
        )
        self.logger = get_logger("openai_handler")
    
    def _load_tags(self) -> List[str]:
        with open("tags.json") as f:
            return json.load(f) 
    
    def _filter_tags(self, generated_tags: List[str]) -> List[str]:
        filtered_tags = []
        for tag in generated_tags:
            if type(tag) is not str:
                self.logger.warning(f"Generated tag that was not a string: {tag}")
            elif tag.lower().strip() not in self.tags_lower:
                self.logger.warning(f"Generated tag that was not predefined: {tag}")
            else:
                filtered_tags.append(tag)
        return filtered_tags

    def summarize_entry(self, entry: RSSEntry) -> AIResponse:
        result: dict = self._get_summary_chain().invoke({
            "title": entry.title,
            "text_content": entry.text_content
        })
        summary = result.get("summary")
        tags = result.get("tags")
        search_query = result.get("search_query")
        if summary is None or type(summary) is not str:
            raise RuntimeError(f"Could not parse summary in summary result: {json.dumps(result)}")
        if tags is None or type(tags) is not list:
            raise RuntimeError(f"Could not parse tags in summary result: {json.dumps(result)}")
        if search_query is None or type(search_query) is not str:
            raise RuntimeError(f"Could not parse query in summary result: {json.dumps(result)}")
        tags = self._filter_tags(tags)
        return AIResponse(summary, tags, search_query)

    def _get_summary_chain(self) -> RunnableSerializable[dict, Any]:
        now = datetime.utcnow().strftime("%Y-%m-%d %H:%M UTC")
        system_message = SystemMessage(
            "You are an extremely experienced IT security news analyst. "
            "Take the following TITLE and ARTICLE and create a short summary "
            "with the key take-aways from the news story. No chit-chat. "
            "Use three sentences maximum and keep the summary concise.\n\n"
            "Furthermore, select at most ten tags from the given list of TAGS "
            "that best describe the topic of the ARTICLE. Use as few tags as possible."
            "Only use the tags given in the list. Never use any other tags.\n\n"
            "Lastly create a single Google search query (search_query) that can be used "
            "to investigate the topic of the news article further. "
            "Focus with the search_query on finding more information from different sources for the information in that article. "
            f"Remember, the current date and time is {now}. "
            "Make sure to create the search_query such that it returns results from multiple sources, "
            "so for example never use the \"site\" keyword.\n\n"
            "Put the summary, the selected tags and the search_query in a JSON object with the following structure: \n"
            "{ \n"
            "    \"summary\": \"<your summary here>\",\n"
            "    \"tags\": [\n"
            "        \"<first tag>\",\n"
            "        \"<second tag>\",\n"
            "        ...\n"
            "    ],\n"
            "    \"search_query\": \"<your Google search query here>\""
            "}\n"
            "Remember to escape special characters that would break the JSON format.\n"
            "For exampe escape the double quote like this: \\\""
        )
        human_message = HumanMessagePromptTemplate.from_template(
            "TITLE: {title} \n"
            "ARTICLE: {text_content}\n"
            f"TAGS: {json.dumps(self.tags)}"
        )
        prompt = ChatPromptTemplate.from_messages([
            system_message, human_message
        ])
        return (
            prompt
            | self.chat_model
            | JsonOutputParser()
        )
