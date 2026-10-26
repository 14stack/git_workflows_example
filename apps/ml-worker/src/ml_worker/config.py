"""Runtime configuration, read from environment variables."""

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(frozen=True)

    rabbitmq_url: str = Field(
        default="amqp://guest:guest@localhost:5672/", validation_alias="RABBITMQ_URL"
    )
    database_url: str = Field(
        default="postgresql://jobs:jobs@localhost:5432/jobs", validation_alias="DATABASE_URL"
    )
    prefetch_count: int = Field(default=1, ge=1, validation_alias="WORKER_PREFETCH_COUNT")
    simulated_delay_seconds: float = Field(
        default=0.0, ge=0.0, le=60.0, validation_alias="WORKER_SIMULATED_DELAY_SECONDS"
    )
    log_level: str = Field(default="INFO", validation_alias="WORKER_LOG_LEVEL")
