"""Consumer side of the contract test (the API has the producer side)."""

import json
from collections.abc import Callable
from pathlib import Path
from typing import Any

import pytest
from jsonschema import Draft202012Validator, FormatChecker

from ml_worker.contracts import Algorithm, Dataset, JobRequested

SCHEMA_PATH = Path(__file__).resolve().parents[4] / "contracts" / "job-requested.v1.schema.json"


@pytest.fixture(scope="module")
def schema() -> dict[str, Any]:
    loaded: dict[str, Any] = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
    return loaded


@pytest.fixture(scope="module")
def validator(schema: dict[str, Any]) -> Draft202012Validator:
    Draft202012Validator.check_schema(schema)
    return Draft202012Validator(schema, format_checker=FormatChecker())


def test_worker_supports_every_dataset_the_producer_may_send(schema: dict[str, Any]) -> None:
    assert set(schema["properties"]["dataset"]["enum"]) == {d.value for d in Dataset}


def test_worker_supports_every_algorithm_the_producer_may_send(schema: dict[str, Any]) -> None:
    assert set(schema["properties"]["algorithm"]["enum"]) == {a.value for a in Algorithm}


def test_worker_model_reads_every_required_field(schema: dict[str, Any]) -> None:
    assert set(schema["required"]) <= set(JobRequested.model_fields)


def test_message_shape_understood_by_worker_is_valid_per_schema(
    validator: Draft202012Validator, make_message: Callable[..., JobRequested]
) -> None:
    document = make_message().model_dump(mode="json")

    errors = [error.message for error in validator.iter_errors(document)]

    assert errors == []


def test_numeric_bounds_match_the_schema(
    schema: dict[str, Any], make_message: Callable[..., JobRequested]
) -> None:
    bounds = schema["properties"]["test_size"]

    make_message(test_size=bounds["minimum"])
    make_message(test_size=bounds["maximum"])
    with pytest.raises(ValueError, match="test_size"):
        make_message(test_size=bounds["maximum"] + 0.01)
