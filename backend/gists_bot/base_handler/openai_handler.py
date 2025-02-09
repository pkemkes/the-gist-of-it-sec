import json
from typing import Any, List
from datetime import datetime, timezone, timedelta
from dacite import from_dict
from dacite.exceptions import DaciteError

from langchain_openai import ChatOpenAI
from langchain_core.messages import SystemMessage
from langchain_core.prompts import HumanMessagePromptTemplate, ChatPromptTemplate
from langchain_core.output_parsers import JsonOutputParser
from langchain_core.runnables import RunnableSerializable
from prometheus_client import Summary, Gauge

from feeds.rss_entry import RSSEntry
from gists_utils.logger import get_logger
from gists_utils.types import AIResponse, Gist, CategoryRecap


SUMMARIZE_ENTRY_SUMMARY = Summary("summarize_entry_seconds", "Time spent summarizing an entry")
DAILY_RECAP_GAUGE = Gauge("generate_daily_recap_seconds", "Time spent generating a daily recap")
WEEKLY_RECAP_GAUGE = Gauge("generate_weekly_recap_seconds", "Time spent generating a weekly recap")


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

    @SUMMARIZE_ENTRY_SUMMARY.time()
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
    
    @staticmethod
    def _datetime_to_readable_timestamp(datetime_to_convert: datetime) -> str:
        return datetime_to_convert.strftime("%Y-%m-%d %H:%M UTC")

    def _get_summary_chain(self) -> RunnableSerializable[dict, Any]:
        now = self._datetime_to_readable_timestamp(datetime.now(timezone.utc))
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
            "For example escape the double quote like this: \\\""
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
    
    @DAILY_RECAP_GAUGE.time()
    def generate_daily_recap(self, gists: list[Gist]) -> list[CategoryRecap]:
        return self.generate_recap(gists, 1)
    
    @WEEKLY_RECAP_GAUGE.time()
    def generate_weeky_recap(self, gists: list[Gist]) -> list[CategoryRecap]:
        return self.generate_recap(gists, 7)

    def generate_recap(self, gists: list[Gist], days: int) -> list[CategoryRecap]:
        result: list = self._get_recap_chain(days).invoke({
            "gists": gists
        })
        if result is None or type(result) is not list:
            raise RuntimeError(f"Could not correctly parse recap of {days} days with result: {json.dumps(result)}")
        recap = []
        for generated_category in result:
            if type(generated_category) is not dict:
                self.logger.warning(f"Category in recap of {days} days is not a dict. Category data: {generated_category}")
                continue
            try:
                recap.append(from_dict(CategoryRecap, generated_category))
            except DaciteError as e:
                self.logger.warning(
                    f"Could not parse category in recap of {days} days. "
                    f"Error: {e}. Category data: {json.dumps(generated_category)}"
                )
        return recap

    def _get_recap_chain(self, days: int) -> RunnableSerializable[dict, Any]:
        now = datetime.now(timezone.utc)
        from_time = self._datetime_to_readable_timestamp(now - timedelta(days=days))
        to_time = self._datetime_to_readable_timestamp(now)
        timeframe_desc = "24 hours" if days == 1 else "7 days"
        system_message = SystemMessage(
            "You are an extremely experienced IT security news analyst. Your task is "
            f"to create a recap of the news of the last {timeframe_desc}. This is the "
            f"timeframe from {from_time} until {to_time}. You will be given the title, "
            "a short summary and the ID number of all relevant news from multiple outlets. "
            "The input will be of the following format:\n"
            "\n"
            "```\n"
            "TITLE: Title of the First News Story\n"
            "SUMMARY: Short summary of the first news story\n"
            "ID: 123\n"
            "\n"
            "TITLE: Title of another News Story\n"
            "SUMMARY: Short summary of another news story\n"
            "ID: 124\n"
            "\n"
            "TITLE: Title of a third News Story\n"
            "SUMMARY: Short summary of a third news story\n"
            "ID: 125\n"
            "\n"
            "...\n"
            "```\n"
            "\n"
            "You will create a recap of the most significant news stories. Group the "
            "news into categories. For each category of news, supply up to five ID numbers "
            "of the news that are related to the respective recap.\n"
            "\n"
            "Provide your response in the following JSON format:\n"
            "\n"
            "```json\n"
            "[\n"
            "  {\n"
            "	\"heading\": \"heading of the first category\",\n"
            "	\"recap\": \"recap of news regarding the topic of the first category\",\n"
            "	\"related\": [185, 195, 201]\n"
            "  },\n"
            "  {\n"
            "	\"heading\": \"heading of the second category\",\n"
            "	\"recap\": \"recap of news regarding the topic of the second category\",\n"
            "	\"related\": [177, 188, 192, 210]\n"
            "  },\n"
            "  ...\n"
            "]\n"
            "```\n"
            "\n"
            "Remember to escape special characters that would break the JSON format. For example "
            "escape the double quote like this: \\\"\n"
            "\n"
            "Be concise and keep your recap without yapping around. Keep it neutral and objective.\n"
        )
        human_message = HumanMessagePromptTemplate.from_template(
            "{% for gist in gists %}\n"
            "TITLE: {{ gist.title }}\n"
            "SUMMARY: {{ gist.summary }}\n"
            "ID: {{ gist.id }}\n\n"
            "{% endfor %}",
            template_format="jinja2"
        )
        prompt = ChatPromptTemplate.from_messages([
            system_message, human_message
        ])
        return (
            prompt
            | self.chat_model
            | JsonOutputParser()
        )
