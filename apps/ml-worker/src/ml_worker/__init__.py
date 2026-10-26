"""ML worker: trains scikit-learn models for jobs requested through RabbitMQ."""

from importlib.metadata import PackageNotFoundError, version

try:
    __version__ = version("ml-worker")
except PackageNotFoundError:  # pragma: no cover - only when running from a raw checkout
    __version__ = "0.0.0"
