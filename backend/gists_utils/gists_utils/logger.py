from logging import Logger, getLogger, StreamHandler, Formatter, INFO


LOGGERS: dict[str, Logger] = dict()


def get_logger(name: str = None) -> Logger:
    if name in LOGGERS:
        return LOGGERS[name]
    formatter = Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
    stream_handler = StreamHandler()
    stream_handler.setFormatter(formatter)
    logger = getLogger(name)
    logger.addHandler(stream_handler)
    logger.setLevel(INFO)
    LOGGERS[name] = logger
    return logger
