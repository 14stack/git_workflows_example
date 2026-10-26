"""Test doubles shared by the unit tests (importable thanks to ``pythonpath = ["tests"]``)."""

from collections.abc import Mapping
from dataclasses import dataclass, field
from datetime import UTC, datetime
from typing import Any
from uuid import UUID

FIXED_NOW = datetime(2026, 1, 15, 10, 30, tzinfo=UTC)


@dataclass
class FakeRepository:
    """In-memory stand-in for PostgresJobRepository that records every call."""

    claimable: bool = True
    calls: list[tuple[str, UUID, Any]] = field(default_factory=list)

    def mark_running(self, job_id: UUID, at: datetime) -> bool:
        self.calls.append(("running", job_id, at))
        return self.claimable

    def mark_succeeded(
        self, job_id: UUID, metrics: Mapping[str, float | int], at: datetime
    ) -> None:
        self.calls.append(("succeeded", job_id, dict(metrics)))

    def mark_failed(self, job_id: UUID, error: str, at: datetime) -> None:
        self.calls.append(("failed", job_id, error))
