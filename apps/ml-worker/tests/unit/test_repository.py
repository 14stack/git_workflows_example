from typing import Any
from uuid import uuid4

import psycopg
import pytest
from psycopg.types.json import Jsonb
from support import FIXED_NOW

from ml_worker.repository import MAX_ERROR_LENGTH, PostgresJobRepository


class FakeCursor:
    def __init__(self, connection: "FakeConnection") -> None:
        self._connection = connection
        self.rowcount = connection.rowcount

    def __enter__(self) -> "FakeCursor":
        return self

    def __exit__(self, *_: object) -> None:
        return None

    def execute(self, sql: str, params: tuple[Any, ...]) -> None:
        if self._connection.fail_with is not None:
            raise self._connection.fail_with
        self._connection.executed.append((sql, params))


class FakeConnection:
    def __init__(self) -> None:
        self.closed = False
        self.rowcount = 1
        self.fail_with: Exception | None = None
        self.executed: list[tuple[str, tuple[Any, ...]]] = []

    def cursor(self) -> FakeCursor:
        return FakeCursor(self)

    def close(self) -> None:
        self.closed = True


@pytest.fixture
def connections(monkeypatch: pytest.MonkeyPatch) -> list[FakeConnection]:
    opened: list[FakeConnection] = []

    def fake_connect(_url: str, *, autocommit: bool) -> FakeConnection:
        assert autocommit
        opened.append(FakeConnection())
        return opened[-1]

    monkeypatch.setattr(psycopg, "connect", fake_connect)
    return opened


def test_mark_running_only_claims_pending_jobs(connections: list[FakeConnection]) -> None:
    repo = PostgresJobRepository("postgresql://test")
    job_id = uuid4()

    assert repo.mark_running(job_id, FIXED_NOW) is True

    sql, params = connections[0].executed[0]
    assert "status = 'running'" in sql
    assert "status = 'pending'" in sql
    assert params == (FIXED_NOW, job_id)


def test_mark_running_returns_false_when_nothing_was_updated(
    connections: list[FakeConnection],
) -> None:
    repo = PostgresJobRepository("postgresql://test")
    repo.mark_running(uuid4(), FIXED_NOW)
    connections[0].rowcount = 0

    assert repo.mark_running(uuid4(), FIXED_NOW) is False


def test_mark_succeeded_stores_metrics_as_jsonb(connections: list[FakeConnection]) -> None:
    repo = PostgresJobRepository("postgresql://test")

    repo.mark_succeeded(uuid4(), {"accuracy": 0.9}, FIXED_NOW)

    _, params = connections[0].executed[0]
    assert isinstance(params[0], Jsonb)
    assert params[0].obj == {"accuracy": 0.9}


def test_mark_failed_truncates_long_errors(connections: list[FakeConnection]) -> None:
    repo = PostgresJobRepository("postgresql://test")

    repo.mark_failed(uuid4(), "x" * (MAX_ERROR_LENGTH + 50), FIXED_NOW)

    _, params = connections[0].executed[0]
    assert len(params[0]) == MAX_ERROR_LENGTH


def test_connection_is_reused_and_reopened_after_operational_error(
    connections: list[FakeConnection],
) -> None:
    repo = PostgresJobRepository("postgresql://test")
    repo.mark_running(uuid4(), FIXED_NOW)
    repo.mark_running(uuid4(), FIXED_NOW)
    assert len(connections) == 1

    connections[0].fail_with = psycopg.OperationalError("server closed the connection")
    with pytest.raises(psycopg.OperationalError):
        repo.mark_running(uuid4(), FIXED_NOW)
    assert connections[0].closed

    repo.mark_running(uuid4(), FIXED_NOW)
    assert len(connections) == 2

    repo.close()
    assert connections[1].closed
