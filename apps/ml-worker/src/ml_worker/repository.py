"""Job persistence. The API owns the schema; the worker only moves jobs through their lifecycle."""

from collections.abc import Iterator, Mapping
from contextlib import contextmanager
from datetime import datetime
from typing import Protocol
from uuid import UUID

import psycopg
from psycopg.types.json import Jsonb

MAX_ERROR_LENGTH = 2_000


class JobRepository(Protocol):
    def mark_running(self, job_id: UUID, at: datetime) -> bool:
        """Claim a pending job. Returns ``False`` if it is not (or no longer) pending."""
        ...

    def mark_succeeded(
        self, job_id: UUID, metrics: Mapping[str, float | int], at: datetime
    ) -> None: ...

    def mark_failed(self, job_id: UUID, error: str, at: datetime) -> None: ...


class PostgresJobRepository:
    """psycopg-backed repository with a lazily (re)opened autocommit connection."""

    def __init__(self, database_url: str) -> None:
        self._database_url = database_url
        self._connection: psycopg.Connection[tuple[object, ...]] | None = None

    def mark_running(self, job_id: UUID, at: datetime) -> bool:
        # The status guard makes redelivered messages harmless (idempotent consumer).
        with self._cursor() as cursor:
            cursor.execute(
                "UPDATE jobs SET status = 'running', started_at = %s "
                "WHERE id = %s AND status = 'pending'",
                (at, job_id),
            )
            return cursor.rowcount == 1

    def mark_succeeded(
        self, job_id: UUID, metrics: Mapping[str, float | int], at: datetime
    ) -> None:
        with self._cursor() as cursor:
            cursor.execute(
                "UPDATE jobs "
                "SET status = 'succeeded', metrics = %s, error = NULL, completed_at = %s "
                "WHERE id = %s AND status = 'running'",
                (Jsonb(dict(metrics)), at, job_id),
            )

    def mark_failed(self, job_id: UUID, error: str, at: datetime) -> None:
        with self._cursor() as cursor:
            cursor.execute(
                "UPDATE jobs SET status = 'failed', error = %s, completed_at = %s "
                "WHERE id = %s AND status = 'running'",
                (error[:MAX_ERROR_LENGTH], at, job_id),
            )

    def close(self) -> None:
        if self._connection is not None and not self._connection.closed:
            self._connection.close()

    @contextmanager
    def _cursor(self) -> Iterator[psycopg.Cursor[tuple[object, ...]]]:
        if self._connection is None or self._connection.closed:
            self._connection = psycopg.connect(self._database_url, autocommit=True)
        try:
            with self._connection.cursor() as cursor:
                yield cursor
        except psycopg.OperationalError:
            # Drop the broken connection; the next call reconnects.
            self.close()
            self._connection = None
            raise
