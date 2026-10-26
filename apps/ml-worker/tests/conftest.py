from collections.abc import Callable
from typing import Any
from uuid import uuid4

import pytest
from support import FIXED_NOW, FakeRepository

from ml_worker.contracts import Algorithm, Dataset, JobRequested


@pytest.fixture
def repository() -> FakeRepository:
    return FakeRepository()


@pytest.fixture
def make_message() -> Callable[..., JobRequested]:
    def factory(**overrides: Any) -> JobRequested:
        payload: dict[str, Any] = {
            "message_id": uuid4(),
            "message_type": "job.requested",
            "schema_version": 1,
            "occurred_at": FIXED_NOW,
            "job_id": uuid4(),
            "name": "Iris forest",
            "dataset": Dataset.IRIS,
            "algorithm": Algorithm.RANDOM_FOREST,
            "test_size": 0.2,
        }
        payload.update(overrides)
        return JobRequested.model_validate(payload)

    return factory
