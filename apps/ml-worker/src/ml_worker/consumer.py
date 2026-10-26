"""RabbitMQ plumbing.

Ack/reject/requeue decisions are made by :class:`MessageProcessor`, which has no pika dependency.
"""

import functools
import logging
import time
from collections.abc import Callable
from enum import StrEnum
from typing import Any, Protocol

import pika
from pydantic import ValidationError

from ml_worker import topology
from ml_worker.contracts import JobRequested
from ml_worker.handler import HandleOutcome, JobHandler

logger = logging.getLogger(__name__)

REQUEUE_DELAY_SECONDS = 2.0


class Decision(StrEnum):
    ACK = "ack"
    REJECT = "reject"  # Poison message: dead-letter it, never retry.
    REQUEUE = "requeue"  # Transient failure (e.g. database down): retry later.


class Handler(Protocol):
    def handle(self, message: JobRequested) -> HandleOutcome: ...


class MessageProcessor:
    def __init__(self, handler: Handler) -> None:
        self._handler = handler

    def process(self, body: bytes) -> Decision:
        try:
            message = JobRequested.model_validate_json(body)
        except ValidationError as exc:
            logger.error("Rejecting invalid message: %s", exc.errors(include_url=False))
            return Decision.REJECT

        try:
            self._handler.handle(message)
        except Exception:
            # Training errors are recorded by the handler, so anything that reaches this point is
            # infrastructure. Quorum queues dead-letter a message after their delivery limit,
            # so this cannot loop forever.
            logger.exception("Transient failure while handling job %s", message.job_id)
            return Decision.REQUEUE

        return Decision.ACK


def on_message(
    processor: MessageProcessor,
    channel: Any,
    method: Any,
    _properties: Any,
    body: bytes,
    sleep: Callable[[float], None] = time.sleep,
) -> None:
    """pika ``on_message_callback``: translate the processor's decision into an AMQP ack/nack."""
    match processor.process(body):
        case Decision.ACK:
            channel.basic_ack(delivery_tag=method.delivery_tag)
        case Decision.REJECT:
            channel.basic_reject(delivery_tag=method.delivery_tag, requeue=False)
        case Decision.REQUEUE:
            sleep(REQUEUE_DELAY_SECONDS)
            channel.basic_nack(delivery_tag=method.delivery_tag, requeue=True)


class Consumer:
    """Blocking consumer bound to the worker queue."""

    def __init__(self, rabbitmq_url: str, prefetch_count: int, handler: JobHandler) -> None:
        self._parameters = pika.URLParameters(rabbitmq_url)
        self._prefetch_count = prefetch_count
        self._processor = MessageProcessor(handler)
        self._channel: Any = None

    def run(self) -> None:
        connection = pika.BlockingConnection(self._parameters)
        try:
            self._channel = connection.channel()
            topology.declare(self._channel)
            self._channel.basic_qos(prefetch_count=self._prefetch_count)
            self._channel.basic_consume(
                topology.WORKER_QUEUE,
                on_message_callback=functools.partial(on_message, self._processor),
            )
            logger.info("Consuming from %s", topology.WORKER_QUEUE)
            self._channel.start_consuming()
        finally:
            # pika's `is_open` is a property, not a method.
            if connection.is_open:  # nosemgrep: is-function-without-parentheses
                connection.close()

    def stop(self) -> None:
        # pika's `is_open` is a property, not a method.
        # nosemgrep: is-function-without-parentheses
        if self._channel is not None and self._channel.is_open:
            self._channel.stop_consuming()
