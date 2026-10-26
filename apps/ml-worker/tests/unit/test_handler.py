from collections.abc import Callable

from support import FIXED_NOW, FakeRepository

from ml_worker.contracts import Algorithm, Dataset, JobRequested
from ml_worker.handler import HandleOutcome, JobHandler
from ml_worker.training import TrainingResult

RESULT = TrainingResult(
    accuracy=0.97, f1_macro=0.96, train_samples=120, test_samples=30, duration_ms=5
)


def fake_trainer(_dataset: Dataset, _algorithm: Algorithm, _test_size: float) -> TrainingResult:
    return RESULT


def failing_trainer(_dataset: Dataset, _algorithm: Algorithm, _test_size: float) -> TrainingResult:
    raise RuntimeError("out of memory")


def make_handler(repository: FakeRepository, **kwargs: object) -> JobHandler:
    return JobHandler(repository, clock=lambda: FIXED_NOW, **kwargs)  # type: ignore[arg-type]


def test_successful_job_is_claimed_then_completed_with_metrics(
    repository: FakeRepository, make_message: Callable[..., JobRequested]
) -> None:
    message = make_message()

    outcome = make_handler(repository, trainer=fake_trainer).handle(message)

    assert outcome is HandleOutcome.SUCCEEDED
    assert repository.calls == [
        ("running", message.job_id, FIXED_NOW),
        ("succeeded", message.job_id, RESULT.as_metrics()),
    ]


def test_trainer_receives_job_parameters(
    repository: FakeRepository, make_message: Callable[..., JobRequested]
) -> None:
    received: list[tuple[Dataset, Algorithm, float]] = []

    def spy(dataset: Dataset, algorithm: Algorithm, test_size: float) -> TrainingResult:
        received.append((dataset, algorithm, test_size))
        return RESULT

    message = make_message(dataset="wine", algorithm="svm", test_size=0.35)
    make_handler(repository, trainer=spy).handle(message)

    assert received == [(Dataset.WINE, Algorithm.SVM, 0.35)]


def test_job_that_is_no_longer_pending_is_skipped(
    repository: FakeRepository, make_message: Callable[..., JobRequested]
) -> None:
    repository.claimable = False

    outcome = make_handler(repository, trainer=fake_trainer).handle(make_message())

    assert outcome is HandleOutcome.SKIPPED
    assert [call[0] for call in repository.calls] == ["running"]


def test_training_error_marks_job_failed_instead_of_crashing(
    repository: FakeRepository, make_message: Callable[..., JobRequested]
) -> None:
    message = make_message()

    outcome = make_handler(repository, trainer=failing_trainer).handle(message)

    assert outcome is HandleOutcome.FAILED
    assert repository.calls[-1] == ("failed", message.job_id, "RuntimeError: out of memory")


def test_simulated_delay_is_applied_only_when_configured(
    repository: FakeRepository, make_message: Callable[..., JobRequested]
) -> None:
    sleeps: list[float] = []

    make_handler(repository, trainer=fake_trainer, sleep=sleeps.append).handle(make_message())
    make_handler(
        repository, trainer=fake_trainer, sleep=sleeps.append, simulated_delay_seconds=1.5
    ).handle(make_message())

    assert sleeps == [1.5]
