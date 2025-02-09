from datetime import datetime, timezone, timedelta
from prometheus_client import Gauge

from base_handler.mariadb_gists_handler import MariaDbGistsHandler
from base_handler.openai_handler import OpenAIHandler
from gists_utils.logger import get_logger


DAILY_RECAP_GAUGE = Gauge("create_daily_recap_seconds", "Time spent to create a daily recap")
WEEKLY_RECAP_GAUGE = Gauge("create_weekly_recap_seconds", "Time spent to create a weekly recap")

class RecapHandler:
	def __init__(self, db: MariaDbGistsHandler, ai: OpenAIHandler):
		self._db = db
		self._ai = ai
		self._logger = get_logger("recap_handler")
		self._utc_hour_to_create_after = 5

	@staticmethod
	def get_utc_now() -> datetime:
		return datetime.now(timezone.utc)

	def recap_if_necessary(self) -> None:
		daily_is_necessary, weekly_is_necessary = self._recap_necessary()
		if daily_is_necessary:
			self._logger.info("Daily recap necessary")
			self._create_daily_recap()
		if weekly_is_necessary:
			self._logger.info("Weekly recap necessary")
			self._create_weekly_recap()
	
	def _recap_necessary(self) -> tuple[bool, bool]:
		now = self.get_utc_now()
		is_time_to_create = now.hour >= self._utc_hour_to_create_after
		if not is_time_to_create:
			return False, False
		daily_is_created_too_long_ago = (now - self._db.get_last_daily_recap_created()) > timedelta(days=1)
		weekly_is_created_too_long_ago = (now - self._db.get_last_weekly_recap_created()) > timedelta(days=1)
		return (daily_is_created_too_long_ago, weekly_is_created_too_long_ago)
	
	@DAILY_RECAP_GAUGE.time()
	def _create_daily_recap(self) -> None:
		gists = self._db.get_gists_of_last_day()
		recap = self._ai.generate_daily_recap(gists)
		self._db.insert_daily_recap(recap)
		self._logger.info("Daily recap created")
	
	@WEEKLY_RECAP_GAUGE.time()
	def _create_weekly_recap(self) -> None:
		gists = self._db.get_gists_of_last_week()
		recap = self._ai.generate_weeky_recap(gists)
		self._db.insert_weekly_recap(recap)
		self._logger.info("Weekly recap created")
		