"""Business logic for one job: claim it, train, record the outcome."""

import logging
import time
from collections.abc import Callable
from datetime import UTC, datetime
from enum import StrEnum

from ml_worker.contracts import Algorithm, Dataset, JobRequested
from ml_worker.repository import JobRepository
from ml_worker.training import TrainingResult, train_and_evaluate

logger = logging.getLogger(__name__)

Trainer = Callable[[Dataset, Algorithm, float], TrainingResult]


class HandleOutcome(StrEnum):
    SUCCEEDED = "succeeded"
    FAILED = "failed"
    SKIPPED = "skipped"


def utc_now() -> datetime:
    return datetime.now(UTC)


class JobHandler:
    def __init__(
        self,
        repository: JobRepository,
        trainer: Trainer = train_and_evaluate,
        clock: Callable[[], datetime] = utc_now,
        sleep: Callable[[float], None] = time.sleep,
        simulated_delay_seconds: float = 0.0,
    ) -> None:
        self._repository = repository
        self._trainer = trainer
        self._clock = clock
        self._sleep = sleep
        self._simulated_delay_seconds = simulated_delay_seconds

    def handle(self, message: JobRequested) -> HandleOutcome:
        job_id = message.job_id

        if not self._repository.mark_running(job_id, self._clock()):
            logger.info("Skipping job %s: it is not pending (duplicate delivery?)", job_id)
            return HandleOutcome.SKIPPED

        logger.info("Training job %s: %s on %s", job_id, message.algorithm, message.dataset)

        if self._simulated_delay_seconds > 0:
            # Demo only: makes the "running" state visible in the UI.
            self._sleep(self._simulated_delay_seconds)

        try:
            result = self._trainer(message.dataset, message.algorithm, message.test_size)
        except Exception as exc:  # A bad job must never take the worker down.
            logger.exception("Job %s failed", job_id)
            self._repository.mark_failed(job_id, f"{type(exc).__name__}: {exc}", self._clock())
            return HandleOutcome.FAILED

        self._repository.mark_succeeded(job_id, result.as_metrics(), self._clock())
        logger.info("Job %s succeeded with accuracy %.4f", job_id, result.accuracy)
        return HandleOutcome.SUCCEEDED
