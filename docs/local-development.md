# Local development

## Prerequisites

| Tool | Version | Used for |
|---|---|---|
| [Docker](https://docs.docker.com/get-docker/) with Compose v2 + Buildx | recent | the stack, integration tests, security scans |
| [just](https://just.systems/man/en/packages.html) | ≥ 1.38 | every command in this repo |
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0.x | jobs-api |
| [uv](https://docs.astral.sh/uv/getting-started/installation/) | ≥ 0.11 | ml-worker (uv installs Python 3.13 itself) |
| [Node.js](https://nodejs.org/) | 24 LTS (see `apps/web/.nvmrc`) | web |

Only Docker and `just` are needed to **run** the stack. The SDKs are needed to build and
test outside containers.

## First run

```bash
just bootstrap   # restore/sync/install all apps, Playwright browser, git hooks
just up          # build images and start everything in the background
```

Then open:

| URL | What |
|---|---|
| http://localhost:8080 | UI (and `/api/*` through nginx) |
| http://localhost:5080/openapi/v1.json | API OpenAPI document (Development only, i.e. `just api-run`) |
| http://localhost:5080/health/ready | API readiness (PostgreSQL + RabbitMQ) |
| http://localhost:15672 | RabbitMQ management (guest / guest) |
| `localhost:5432` | PostgreSQL (jobs / jobs) |

Ports and credentials can be overridden by copying `deploy/compose/.env.example` to
`deploy/compose/.env`.

```bash
just ps            # container status
just logs ml-worker  # follow one service (or several, or all)
just down          # stop, keep data
just reset         # stop and delete the database volume
```

## Working on one component

Start the infrastructure in Docker and run the component you are changing natively, with hot
reload and a debugger:

```bash
just up postgres rabbitmq db-migrate   # infra + schema only
just api-run      # API on http://localhost:5080
just worker-run   # worker against the same broker/database
just web-run      # Angular dev server on http://localhost:4200, proxies /api to :5080
```

## Tests and checks

```bash
just api-test-unit          # fast, no Docker
just api-test-integration   # Testcontainers: needs Docker
just worker-test-unit
just web-test-unit
just web-test-e2e           # builds and starts the full stack, then runs Playwright

just fmt                    # apply all formatters
just lint                   # what the Lint & Format workflows check
just ci                     # everything CI runs
```

Reports, coverage and Playwright traces end up in `artifacts/` (gitignored). To inspect a
failed e2e run: `npx playwright show-report artifacts/test-results/web-e2e/html` (from `apps/web`).

## Git hooks

`just bootstrap` (or `just hooks-install`) installs [pre-commit](https://pre-commit.com) hooks:

- **pre-commit:** whitespace/EOF/YAML/JSON/TOML checks, Gitleaks, actionlint + zizmor for
  workflow changes, and, **only for the apps you changed**, the format check and unit tests.
- **commit-msg:** rejects messages that are not [Conventional Commits](https://www.conventionalcommits.org/).

Run them all on demand with `just hooks-run`. In an emergency, `git commit --no-verify` skips
them, but CI runs the same checks anyway.

## Database migrations

```bash
just api-migration-add AddJobPriority   # creates a migration under Infrastructure/Persistence/Migrations
just up --build db-migrate              # rebuild and apply it to the compose database
```

Migrations are generated code. Commit them, and never edit an applied migration.

## Troubleshooting

| Problem | Fix |
|---|---|
| `just up-wait` times out | `just logs` shows why. `db-migrate` must exit 0 before the API and worker start |
| Port already in use | Override the port in `deploy/compose/.env` |
| `uv lock --check` fails in `worker-lint` | You edited `pyproject.toml`: run `uv lock` in `apps/ml-worker` and commit `uv.lock` |
| `NU1004` / locked-mode restore error in CI | Run `dotnet restore` locally and commit the updated `packages.lock.json` files |
| Playwright cannot find a browser | `just web-e2e-install` |
| Security scans fail behind a corporate proxy | Set `TOOL_DOCKER_ARGS`, see [security.md](security.md#running-the-scans-locally) |
