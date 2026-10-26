import itertools

import pytest

from ml_worker.contracts import Algorithm, Dataset
from ml_worker.training import build_model, train_and_evaluate

DATASET_SIZES = {Dataset.IRIS: 150, Dataset.WINE: 178, Dataset.BREAST_CANCER: 569}


@pytest.mark.parametrize(("dataset", "algorithm"), list(itertools.product(Dataset, Algorithm)))
def test_every_dataset_and_algorithm_trains_a_useful_model(
    dataset: Dataset, algorithm: Algorithm
) -> None:
    result = train_and_evaluate(dataset, algorithm, test_size=0.25)

    # These datasets are easy; anything below 80 % means the pipeline is broken.
    assert 0.8 <= result.accuracy <= 1.0
    assert 0.8 <= result.f1_macro <= 1.0
    assert result.train_samples + result.test_samples == DATASET_SIZES[dataset]
    assert result.duration_ms >= 0


def test_training_is_deterministic_for_a_fixed_random_state() -> None:
    first = train_and_evaluate(Dataset.WINE, Algorithm.RANDOM_FOREST, test_size=0.3)
    second = train_and_evaluate(Dataset.WINE, Algorithm.RANDOM_FOREST, test_size=0.3)

    assert (first.accuracy, first.f1_macro) == (second.accuracy, second.f1_macro)


def test_test_size_controls_the_hold_out_split() -> None:
    result = train_and_evaluate(Dataset.IRIS, Algorithm.LOGISTIC_REGRESSION, test_size=0.2)

    assert result.test_samples == 30
    assert result.train_samples == 120


@pytest.mark.parametrize("test_size", [0.0, 1.0, -0.1])
def test_invalid_test_size_is_rejected(test_size: float) -> None:
    with pytest.raises(ValueError, match="test_size"):
        train_and_evaluate(Dataset.IRIS, Algorithm.SVM, test_size=test_size)


def test_models_scale_features_before_the_estimator() -> None:
    pipeline = build_model(Algorithm.SVM)

    assert [name for name, _ in pipeline.steps] == ["standardscaler", "svc"]


def test_metrics_document_matches_what_the_api_reads() -> None:
    metrics = train_and_evaluate(Dataset.IRIS, Algorithm.SVM, test_size=0.2).as_metrics()

    assert set(metrics) == {"accuracy", "f1_macro", "train_samples", "test_samples", "duration_ms"}
