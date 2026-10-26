# ml-worker

Consumes `job.requested` messages from RabbitMQ, trains a scikit-learn classifier and
writes the job result to PostgreSQL. See [`docs/architecture.md`](../../docs/architecture.md).

Everything runs through the root `justfile`:

```bash
just worker-sync        # install dependencies (uv)
just worker-lint        # ruff format --check, ruff check, mypy, uv lock --check
just worker-test-unit   # pytest + coverage gate
just worker-run         # run against the compose stack's Postgres and RabbitMQ
```

Configuration comes from environment variables (see `src/ml_worker/config.py`):

| Variable | Default |
|---|---|
| `RABBITMQ_URL` | `amqp://guest:guest@localhost:5672/` |
| `DATABASE_URL` | `postgresql://jobs:jobs@localhost:5432/jobs` |
| `WORKER_PREFETCH_COUNT` | `1` |
| `WORKER_SIMULATED_DELAY_SECONDS` | `0` (compose uses `2`, so the UI shows the `running` state) |
| `WORKER_LOG_LEVEL` | `INFO` |
