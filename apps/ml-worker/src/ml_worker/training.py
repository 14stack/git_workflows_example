"""Pure model training: no I/O, fully deterministic for a given ``random_state``."""

from collections.abc import Callable
from dataclasses import dataclass
from time import perf_counter
from typing import Any

from sklearn.datasets import load_breast_cancer, load_iris, load_wine
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import accuracy_score, f1_score
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline, make_pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.svm import SVC

from ml_worker.contracts import Algorithm, Dataset

DEFAULT_RANDOM_STATE = 42

# All three datasets ship with scikit-learn, so training never needs network access.
_LOADERS: dict[Dataset, Callable[..., Any]] = {
    Dataset.IRIS: load_iris,
    Dataset.WINE: load_wine,
    Dataset.BREAST_CANCER: load_breast_cancer,
}


@dataclass(frozen=True, slots=True)
class TrainingResult:
    accuracy: float
    f1_macro: float
    train_samples: int
    test_samples: int
    duration_ms: int

    def as_metrics(self) -> dict[str, float | int]:
        """JSON document stored in ``jobs.metrics`` (read by the API as ``JobMetrics``)."""
        return {
            "accuracy": self.accuracy,
            "f1_macro": self.f1_macro,
            "train_samples": self.train_samples,
            "test_samples": self.test_samples,
            "duration_ms": self.duration_ms,
        }


def build_model(algorithm: Algorithm, random_state: int = DEFAULT_RANDOM_STATE) -> Pipeline:
    match algorithm:
        case Algorithm.LOGISTIC_REGRESSION:
            estimator: Any = LogisticRegression(max_iter=1_000, random_state=random_state)
        case Algorithm.RANDOM_FOREST:
            estimator = RandomForestClassifier(n_estimators=100, random_state=random_state)
        case Algorithm.SVM:
            estimator = SVC(kernel="rbf", random_state=random_state)
    return make_pipeline(StandardScaler(), estimator)


def train_and_evaluate(
    dataset: Dataset,
    algorithm: Algorithm,
    test_size: float,
    random_state: int = DEFAULT_RANDOM_STATE,
) -> TrainingResult:
    """Train ``algorithm`` on a stratified split of ``dataset`` and score it on the hold-out set."""
    if not 0.0 < test_size < 1.0:
        raise ValueError(f"test_size must be between 0 and 1, got {test_size}")

    features, labels = _LOADERS[dataset](return_X_y=True)
    x_train, x_test, y_train, y_test = train_test_split(
        features, labels, test_size=test_size, random_state=random_state, stratify=labels
    )

    started = perf_counter()
    model = build_model(algorithm, random_state)
    model.fit(x_train, y_train)
    predictions = model.predict(x_test)
    duration_ms = round((perf_counter() - started) * 1_000)

    return TrainingResult(
        accuracy=round(float(accuracy_score(y_test, predictions)), 4),
        f1_macro=round(float(f1_score(y_test, predictions, average="macro")), 4),
        train_samples=len(x_train),
        test_samples=len(x_test),
        duration_ms=duration_ms,
    )
