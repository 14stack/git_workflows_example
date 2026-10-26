"""Consumer side of ``contracts/job-requested.v1.schema.json``."""

from datetime import datetime
from enum import StrEnum
from typing import Literal
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class Dataset(StrEnum):
    IRIS = "iris"
    WINE = "wine"
    BREAST_CANCER = "breast_cancer"


class Algorithm(StrEnum):
    LOGISTIC_REGRESSION = "logistic_regression"
    RANDOM_FOREST = "random_forest"
    SVM = "svm"


class JobRequested(BaseModel):
    """A request to train and evaluate one model.

    Unknown fields are ignored so the producer can add optional fields without
    breaking this consumer (see ``contracts/README.md``).
    """

    model_config = ConfigDict(extra="ignore", frozen=True)

    message_id: UUID
    message_type: Literal["job.requested"]
    schema_version: Literal[1]
    occurred_at: datetime
    job_id: UUID
    name: str = Field(min_length=3, max_length=100)
    dataset: Dataset
    algorithm: Algorithm
    test_size: float = Field(ge=0.1, le=0.5)
