import pytest
from pydantic import ValidationError

from ml_worker.config import Settings


def test_defaults_target_the_local_compose_stack(monkeypatch: pytest.MonkeyPatch) -> None:
    for name in ("RABBITMQ_URL", "DATABASE_URL", "WORKER_SIMULATED_DELAY_SECONDS"):
        monkeypatch.delenv(name, raising=False)

    settings = Settings()

    assert settings.rabbitmq_url.startswith("amqp://")
    assert settings.database_url.startswith("postgresql://")
    assert settings.prefetch_count == 1
    assert settings.simulated_delay_seconds == 0.0


def test_values_are_read_from_environment(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("RABBITMQ_URL", "amqp://user:pass@rabbitmq:5672/")
    monkeypatch.setenv("WORKER_PREFETCH_COUNT", "4")
    monkeypatch.setenv("WORKER_SIMULATED_DELAY_SECONDS", "2")

    settings = Settings()

    assert settings.rabbitmq_url == "amqp://user:pass@rabbitmq:5672/"
    assert settings.prefetch_count == 4
    assert settings.simulated_delay_seconds == 2.0


def test_invalid_values_fail_fast(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("WORKER_PREFETCH_COUNT", "0")

    with pytest.raises(ValidationError):
        Settings()
