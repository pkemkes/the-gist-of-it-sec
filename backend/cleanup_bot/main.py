from prometheus_client import start_http_server

from gists_utils.run_in_loop import run_in_loop
from cleanup_bot import CleanUpBot


def run_cleanup(cleanup_bot: CleanUpBot) -> None:
	cleanup_bot.cleanup_gists()


def main():
	start_http_server(9090)
	cleanup_bot = CleanUpBot()
	run_in_loop(run_cleanup, [cleanup_bot], 60*10)


if __name__ == "__main__":
	main()
