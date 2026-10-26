import json
from collections.abc import Callable
from types import SimpleNamespace
from unittest.mock import MagicMock

import pytest

from ml_worker import topology
from ml_worker.consumer import REQUEUE_DELAY_SECONDS, Decision, MessageProcessor, on_message
from ml_worker.contracts import JobRequested
from ml_worker.handler import HandleOutcome


class StubHandler:
    def __init__(self, error: Exception | None = None) -> None:
        self.error = error
        self.handled: list[JobRequested] = []

    def handle(self, message: JobRequested) -> HandleOutcome:
        if self.error is not None:
            raise self.error
        self.handled.append(message)
        return HandleOutcome.SUCCEEDED


def body_of(message: JobRequested) -> bytes:
    return message.model_dump_json().encode()


def test_valid_message_is_handled_and_acked(make_message: Callable[..., JobRequested]) -> None:
    handler = StubHandler()
    message = make_message()

    decision = MessageProcessor(handler).process(body_of(message))

    assert decision is Decision.ACK
    assert handler.handled == [message]


@pytest.mark.parametrize(
    "body",
    [
        b"not json",
        b"{}",
        json.dumps({"message_type": "job.requested", "dataset": "mnist"}).encode(),
    ],
)
def test_invalid_message_is_rejected_without_calling_the_handler(body: bytes) -> None:
    handler = StubHandler()

    assert MessageProcessor(handler).process(body) is Decision.REJECT
    assert handler.handled == []


def test_unknown_fields_are_ignored_for_forward_compatibility(
    make_message: Callable[..., JobRequested],
) -> None:
    payload = json.loads(body_of(make_message()))
    payload["priority"] = "high"

    assert MessageProcessor(StubHandler()).process(json.dumps(payload).encode()) is Decision.ACK


def test_infrastructure_failure_requeues(make_message: Callable[..., JobRequested]) -> None:
    handler = StubHandler(error=ConnectionError("database down"))

    assert MessageProcessor(handler).process(body_of(make_message())) is Decision.REQUEUE


@pytest.mark.parametrize(
    ("decision", "expected_call"),
    [
        (Decision.ACK, ("basic_ack", {"delivery_tag": 7})),
        (Decision.REJECT, ("basic_reject", {"delivery_tag": 7, "requeue": False})),
        (Decision.REQUEUE, ("basic_nack", {"delivery_tag": 7, "requeue": True})),
    ],
)
def test_on_message_translates_decision_to_amqp(
    decision: Decision, expected_call: tuple[str, dict[str, object]]
) -> None:
    processor = MagicMock(spec=MessageProcessor)
    processor.process.return_value = decision
    channel = MagicMock()
    sleeps: list[float] = []

    on_message(
        processor, channel, SimpleNamespace(delivery_tag=7), None, b"{}", sleep=sleeps.append
    )

    name, kwargs = expected_call
    getattr(channel, name).assert_called_once_with(**kwargs)
    assert sleeps == ([REQUEUE_DELAY_SECONDS] if decision is Decision.REQUEUE else [])


def test_topology_declares_dead_lettered_quorum_queue() -> None:
    channel = MagicMock()

    topology.declare(channel)

    channel.queue_declare.assert_any_call(
        topology.WORKER_QUEUE,
        durable=True,
        arguments={
            "x-queue-type": "quorum",
            "x-dead-letter-exchange": topology.DEAD_LETTER_EXCHANGE,
        },
    )
    channel.queue_bind.assert_any_call(
        topology.WORKER_QUEUE, topology.EXCHANGE, routing_key=topology.JOB_REQUESTED_ROUTING_KEY
    )
