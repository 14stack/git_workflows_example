"""Entry point: ``python -m ml_worker`` (or the ``ml-worker`` script)."""

import logging
import signal
import sys
import threading
from types import FrameType

from pika.exceptions import AMQPConnectionError

from ml_worker import __version__
from ml_worker.config import Settings
from ml_worker.consumer import Consumer
from ml_worker.handler import JobHandler
from ml_worker.repository import PostgresJobRepository

logger = logging.getLogger("ml_worker")

MAX_BACKOFF_SECONDS = 30.0


def main() -> int:
    settings = Settings()
    logging.basicConfig(
        level=settings.log_level.upper(),
        format="%(asctime)s %(levelname)s %(name)s %(message)s",
        stream=sys.stdout,
    )
    logging.getLogger("pika").setLevel(logging.WARNING)  # pika is very chatty at INFO
    logger.info("Starting ml-worker %s", __version__)

    repository = PostgresJobRepository(settings.database_url)
    handler = JobHandler(repository, simulated_delay_seconds=settings.simulated_delay_seconds)
    consumer = Consumer(settings.rabbitmq_url, settings.prefetch_count, handler)

    stop = threading.Event()

    def request_stop(signum: int, _frame: FrameType | None) -> None:
        logger.info("Received %s, shutting down", signal.Signals(signum).name)
        stop.set()
        consumer.stop()

    signal.signal(signal.SIGTERM, request_stop)
    signal.signal(signal.SIGINT, request_stop)

    backoff = 1.0
    while not stop.is_set():
        try:
            consumer.run()
            backoff = 1.0
        except AMQPConnectionError:
            logger.warning("RabbitMQ unavailable, retrying in %.0fs", backoff)
            stop.wait(backoff)  # returns early on SIGTERM
            backoff = min(backoff * 2, MAX_BACKOFF_SECONDS)

    repository.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
