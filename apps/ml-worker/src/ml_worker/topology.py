"""Broker topology shared with the API.

Keep in sync with ``contracts/README.md`` and
``apps/jobs-api/src/JobsApi/Infrastructure/Messaging/RabbitMqTopology.cs``: both sides
declare it, and RabbitMQ rejects re-declarations with different arguments.
"""

from typing import Any

EXCHANGE = "jobs"
DEAD_LETTER_EXCHANGE = "jobs.dlx"
JOB_REQUESTED_ROUTING_KEY = "job.requested"
WORKER_QUEUE = "ml-worker.jobs"
WORKER_DEAD_LETTER_QUEUE = "ml-worker.jobs.dlq"


def declare(channel: Any) -> None:
    """Idempotently declare exchanges, queues and bindings on a pika channel."""
    quorum = {"x-queue-type": "quorum"}

    channel.exchange_declare(DEAD_LETTER_EXCHANGE, exchange_type="fanout", durable=True)
    channel.queue_declare(WORKER_DEAD_LETTER_QUEUE, durable=True, arguments=quorum)
    channel.queue_bind(WORKER_DEAD_LETTER_QUEUE, DEAD_LETTER_EXCHANGE, routing_key="")

    channel.exchange_declare(EXCHANGE, exchange_type="direct", durable=True)
    channel.queue_declare(
        WORKER_QUEUE,
        durable=True,
        arguments={**quorum, "x-dead-letter-exchange": DEAD_LETTER_EXCHANGE},
    )
    channel.queue_bind(WORKER_QUEUE, EXCHANGE, routing_key=JOB_REQUESTED_ROUTING_KEY)
