from typing import Callable
from time import time, sleep


def run_in_loop(
        to_be_run: Callable, args: list, timeframe: float
    ) -> None:
    while True:
        started = time()
        to_be_run(*args)
        next_execution = started + timeframe
        now = time()
        if now < next_execution:
            sleep(next_execution - now)
