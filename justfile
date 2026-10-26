# Jobs Platform: the single entry point for building, testing and scanning.
#
# Every GitHub Actions workflow calls exactly one of these recipes, so running
# `just <recipe>` locally reproduces CI. Start with:
#
#   just            # list all recipes, grouped
#   just bootstrap  # install dependencies, browsers and git hooks
#   just up         # run the whole stack with docker compose

set shell := ["bash", "-euo", "pipefail", "-c"]

# ── Paths ────────────────────────────────────────────────────────────────────
root := justfile_directory()
artifacts := root / "artifacts"
compose := "docker compose -f " + root / "deploy/compose/compose.yaml"

# ── Images ───────────────────────────────────────────────────────────────────
registry := env("REGISTRY", "ghcr.io/14stack/git_workflows_example")
images := "jobs-api jobs-api-migrator ml-worker web"

# ── Pinned tools (tag + digest). Bump deliberately, then run `just security`. ──
gitleaks_image := "zricethezav/gitleaks:v8.30.1@sha256:c00b6bd0aeb3071cbcb79009cb16a60dd9e0a7c60e2be9ab65d25e6bc8abbb7f"
trivy_image := "aquasec/trivy:0.76.1@sha256:790fd66d1697395d8693d1acaa1ea702406a20f109b6fc7bac8db623eca6c06c"
semgrep_image := "semgrep/semgrep:1.182.1@sha256:451b3d875c00dac7ed5db5293e584a07de11449a409c10b3c2d2652f429630b6"
actionlint_image := "rhysd/actionlint:1.7.12@sha256:b1934ee5f1c509618f2508e6eb47ee0d3520686341fec936f3b79331f9315667"
zizmor_version := "1.31.1"

# Findings at these severities fail Trivy scans.
trivy_severity := "HIGH,CRITICAL"
trivy_cache := env("TRIVY_CACHE_DIR", env("HOME", "/tmp") / ".cache/trivy")

# Run a containerised tool against the repository as the current user.
# TOOL_DOCKER_ARGS adds extra `docker run` flags, e.g. to mount a corporate CA bundle:
#   TOOL_DOCKER_ARGS="-v /etc/ssl/certs/ca-certificates.crt:/etc/ssl/certs/ca-certificates.crt:ro"
tool_docker_args := env("TOOL_DOCKER_ARGS", "")
docker_tool := "docker run --rm --user \"$(id -u):$(id -g)\" -e HOME=/tmp " + tool_docker_args + " -v \"" + root + ":/src\" -w /src"
trivy := docker_tool + " -e TRIVY_USERNAME -e TRIVY_PASSWORD -e TRIVY_NO_PROGRESS=true -e TRIVY_IGNOREFILE=.trivyignore.yaml -v \"" + trivy_cache + ":/cache\" " + trivy_image + " --cache-dir /cache"
trivy_skip_dirs := "--skip-dirs '**/node_modules' --skip-dirs '**/.venv' --skip-dirs '**/bin' --skip-dirs '**/obj' --skip-dirs artifacts"

# List recipes
default:
    @just --list --unsorted

# ═════════════════════════════════════════════════════════════════════════════
# Setup
# ═════════════════════════════════════════════════════════════════════════════

# Install all dependencies, the Playwright browser and the git hooks
[group('setup')]
bootstrap: api-restore worker-sync web-install web-e2e-install hooks-install
    @echo "✔ Ready. Next: just up"

# Install pre-commit and the pre-commit + commit-msg git hooks
[group('setup')]
hooks-install:
    uv tool install --quiet pre-commit
    pre-commit install --install-hooks --hook-type pre-commit --hook-type commit-msg

# Run every pre-commit hook against all files
[group('setup')]
hooks-run:
    pre-commit run --all-files

# Delete build outputs and reports
[group('setup')]
clean:
    rm -rf "{{ artifacts }}" apps/jobs-api/{src,tests}/*/{bin,obj} apps/web/{dist,.angular,coverage} apps/ml-worker/{.pytest_cache,.mypy_cache,.ruff_cache,.coverage}

# ═════════════════════════════════════════════════════════════════════════════
# Stack (docker compose)
# ═════════════════════════════════════════════════════════════════════════════

# Start the stack in the background (pass --no-build to run already built/pulled images)
[group('stack')]
up *flags="--build":
    {{ compose }} up -d {{ flags }}

# Start the stack and block until the API and web gateway are ready
[group('stack')]
up-wait *flags="--build": (up flags)
    #!/usr/bin/env bash
    set -euo pipefail
    for _ in $(seq 1 90); do
      if curl -fsS "http://localhost:${API_PORT:-5080}/health/ready" >/dev/null 2>&1 \
         && curl -fsS "http://localhost:${WEB_PORT:-8080}/healthz" >/dev/null 2>&1; then
        echo "✔ Stack is ready: http://localhost:${WEB_PORT:-8080}"
        exit 0
      fi
      sleep 2
    done
    echo "✘ Stack did not become ready within 180s" >&2
    {{ compose }} ps -a
    {{ compose }} logs --tail=80
    exit 1

# Stop the stack (data is kept)
[group('stack')]
down:
    {{ compose }} down --remove-orphans

# Stop the stack and delete its volumes
[group('stack')]
reset:
    {{ compose }} down --volumes --remove-orphans

# Follow logs, optionally for specific services
[group('stack')]
logs *services:
    {{ compose }} logs --follow --tail=100 {{ services }}

# Write all container logs to artifacts/compose-logs.txt (used by CI on failure)
[group('stack')]
logs-dump:
    mkdir -p "{{ artifacts }}"
    {{ compose }} ps -a > "{{ artifacts }}/compose-logs.txt"
    {{ compose }} logs --no-color --timestamps >> "{{ artifacts }}/compose-logs.txt"

# Show container status
[group('stack')]
ps:
    {{ compose }} ps -a

# ═════════════════════════════════════════════════════════════════════════════
# Jobs API (.NET)
# ═════════════════════════════════════════════════════════════════════════════

# Restore NuGet packages and local .NET tools
[group('jobs-api')]
[working-directory('apps/jobs-api')]
api-restore:
    dotnet restore JobsApi.slnx
    dotnet tool restore

# Build the solution in Release mode
[group('jobs-api')]
[working-directory('apps/jobs-api')]
api-build:
    dotnet build JobsApi.slnx -c Release

# Apply code formatting and analyzer fixes
[group('jobs-api')]
[working-directory('apps/jobs-api')]
api-format:
    dotnet format JobsApi.slnx

# Check formatting only (fast; used by the pre-commit hook)
[group('jobs-api')]
[working-directory('apps/jobs-api')]
api-format-check:
    dotnet format JobsApi.slnx --verify-no-changes --verbosity minimal

# Check formatting and build with all analyzers as errors
[group('jobs-api')]
[working-directory('apps/jobs-api')]
api-lint: api-format-check
    dotnet build JobsApi.slnx -c Release -warnaserror

# Run unit tests with coverage
[group('jobs-api')]
[working-directory('apps/jobs-api')]
api-test-unit:
    dotnet test --project tests/JobsApi.UnitTests -c Release \
      --results-directory "{{ artifacts }}/test-results/jobs-api-unit" \
      --report-xunit-trx --report-xunit-trx-filename jobs-api-unit.trx \
      --coverlet --coverlet-output-format cobertura \
      --coverlet-include "[JobsApi]*" \
      --coverlet-exclude "[JobsApi]JobsApi.Infrastructure.Persistence.Migrations.*"

# Run integration tests (Testcontainers: needs a Docker daemon)
[group('jobs-api')]
[working-directory('apps/jobs-api')]
api-test-integration:
    dotnet test --project tests/JobsApi.IntegrationTests -c Release \
      --results-directory "{{ artifacts }}/test-results/jobs-api-integration" \
      --report-xunit-trx --report-xunit-trx-filename jobs-api-integration.trx \
      --coverlet --coverlet-output-format cobertura \
      --coverlet-include "[JobsApi]*" \
      --coverlet-exclude "[JobsApi]JobsApi.Infrastructure.Persistence.Migrations.*"

# Run the API locally against the compose Postgres and RabbitMQ (`just up postgres rabbitmq db-migrate`)
[group('jobs-api')]
[working-directory('apps/jobs-api')]
api-run:
    dotnet run --project src/JobsApi --launch-profile http

# Add an EF Core migration, e.g. `just api-migration-add AddJobPriority`
[group('jobs-api')]
[working-directory('apps/jobs-api')]
api-migration-add name:
    dotnet ef migrations add {{ name }} --project src/JobsApi --output-dir Infrastructure/Persistence/Migrations

# ═════════════════════════════════════════════════════════════════════════════
# ML worker (Python)
# ═════════════════════════════════════════════════════════════════════════════

# Create the virtualenv and install locked dependencies
[group('ml-worker')]
[working-directory('apps/ml-worker')]
worker-sync:
    uv sync --locked

# Apply formatting and safe lint fixes
[group('ml-worker')]
[working-directory('apps/ml-worker')]
worker-format:
    uv run --locked ruff format .
    uv run --locked ruff check --fix .

# Check formatting only (fast; used by the pre-commit hook)
[group('ml-worker')]
[working-directory('apps/ml-worker')]
worker-format-check:
    uv run --locked ruff format --check .

# Check formatting, lint, types and that uv.lock is up to date
[group('ml-worker')]
[working-directory('apps/ml-worker')]
worker-lint: worker-format-check
    uv lock --check
    uv run --locked ruff check .
    uv run --locked mypy

# Run unit tests with coverage (fails under 80 %)
[group('ml-worker')]
[working-directory('apps/ml-worker')]
worker-test-unit:
    uv run --locked pytest --cov --cov-report=term \
      --cov-report="xml:{{ artifacts }}/coverage/ml-worker/coverage.cobertura.xml" \
      --junitxml="{{ artifacts }}/test-results/ml-worker-unit/junit.xml"

# Run the worker locally against the compose Postgres and RabbitMQ
[group('ml-worker')]
[working-directory('apps/ml-worker')]
worker-run:
    WORKER_SIMULATED_DELAY_SECONDS="${WORKER_SIMULATED_DELAY_SECONDS:-2}" uv run --locked ml-worker

# ═════════════════════════════════════════════════════════════════════════════
# Web (Angular)
# ═════════════════════════════════════════════════════════════════════════════

# Install npm dependencies from package-lock.json
[group('web')]
[working-directory('apps/web')]
web-install:
    npm ci --no-audit --no-fund

# Apply Prettier formatting
[group('web')]
[working-directory('apps/web')]
web-format:
    npx prettier --write .

# Check formatting only (fast; used by the pre-commit hook)
[group('web')]
[working-directory('apps/web')]
web-format-check:
    npx prettier --check .

# Check formatting and run ESLint (no warnings allowed)
[group('web')]
[working-directory('apps/web')]
web-lint: web-format-check
    npx ng lint --max-warnings 0

# Run unit tests (Vitest) with coverage thresholds
[group('web')]
[working-directory('apps/web')]
web-test-unit:
    npx ng test --watch=false --coverage \
      --reporters=default --reporters=junit \
      --output-file="{{ artifacts }}/test-results/web-unit/junit.xml"

# Production build
[group('web')]
[working-directory('apps/web')]
web-build:
    npx ng build

# Serve the UI with live reload, proxying /api to http://localhost:5080
[group('web')]
[working-directory('apps/web')]
web-run:
    npx ng serve

# Install the Playwright browser (and its OS packages on Linux)
[group('web')]
[working-directory('apps/web')]
web-e2e-install:
    npx playwright install --with-deps chromium

# Run Playwright end-to-end tests against the full compose stack (starts it if needed)
[group('web')]
[working-directory('apps/web')]
web-test-e2e *stack_flags="--build": (up-wait stack_flags)
    npx playwright test

# ═════════════════════════════════════════════════════════════════════════════
# Security (all tools are free / open source and run in pinned containers)
# ═════════════════════════════════════════════════════════════════════════════

# Gitleaks: scan the full git history for committed secrets
[group('security')]
security-secrets:
    mkdir -p "{{ artifacts }}/security"
    {{ docker_tool }} {{ gitleaks_image }} git . \
      --redact --verbose --no-banner \
      --report-format sarif --report-path artifacts/security/gitleaks.sarif

# Semgrep CE: static analysis (SAST) of C#, Python, TypeScript and Dockerfiles
[group('security')]
security-sast:
    mkdir -p "{{ artifacts }}/security"
    {{ docker_tool }} -e SEMGREP_SEND_METRICS=off {{ semgrep_image }} semgrep scan \
      --config p/default --config p/csharp --config p/python \
      --config p/typescript --config p/dockerfile \
      --metrics=off --error --text \
      --sarif-output=artifacts/security/semgrep.sarif

# Trivy: known vulnerabilities in NuGet, PyPI and npm lockfiles
[group('security')]
security-deps:
    #!/usr/bin/env bash
    set -euo pipefail
    mkdir -p "{{ artifacts }}/security" "{{ trivy_cache }}"
    status=0
    {{ trivy }} fs . {{ trivy_skip_dirs }} \
      --scanners vuln --severity {{ trivy_severity }} --ignore-unfixed --exit-code 1 \
      --format json --output artifacts/security/trivy-deps.json || status=$?
    {{ trivy }} convert --format table --scanners vuln artifacts/security/trivy-deps.json
    exit "${status}"

# Trivy: Dockerfile misconfigurations + OS/library CVEs in every built image
[group('security')]
security-images: images-build
    #!/usr/bin/env bash
    set -euo pipefail
    mkdir -p "{{ artifacts }}/security" "{{ artifacts }}/images" "{{ trivy_cache }}"
    status=0
    echo "── Dockerfiles"
    {{ trivy }} config apps --severity {{ trivy_severity }} --exit-code 1 || status=1
    for image in {{ images }}; do
      echo "── Image ${image}"
      docker save "{{ registry }}/${image}:dev" -o "{{ artifacts }}/images/${image}.tar"
      {{ trivy }} image --input "artifacts/images/${image}.tar" \
        --scanners vuln --severity {{ trivy_severity }} --ignore-unfixed --exit-code 1 \
        --format json --output "artifacts/security/trivy-image-${image}.json" || status=1
      {{ trivy }} convert --format table --scanners vuln "artifacts/security/trivy-image-${image}.json"
      rm -f "{{ artifacts }}/images/${image}.tar"
    done
    exit "${status}"

# Run every security scan
[group('security')]
security: security-secrets security-sast security-deps security-images

# ═════════════════════════════════════════════════════════════════════════════
# Images & release
# ═════════════════════════════════════════════════════════════════════════════

# Build all images locally (tag :dev) with docker buildx bake
[group('release')]
images-build:
    VERSION=dev REGISTRY="{{ registry }}" docker buildx bake --load

# Build and push all images for a release, with SBOM + provenance attestations
[group('release')]
images-publish version:
    VERSION="{{ version }}" REGISTRY="{{ registry }}" REVISION="$(git rev-parse HEAD)" \
      docker buildx bake --push \
      --set "*.attest=type=sbom" --set "*.attest=type=provenance,mode=max"

# Export CycloneDX SBOMs of published images to artifacts/sbom (registry auth: TRIVY_USERNAME/TRIVY_PASSWORD)
[group('release')]
images-sbom version:
    #!/usr/bin/env bash
    set -euo pipefail
    mkdir -p "{{ artifacts }}/sbom" "{{ trivy_cache }}"
    for image in {{ images }}; do
      {{ trivy }} image "{{ registry }}/${image}:{{ version }}" \
        --format cyclonedx --output "artifacts/sbom/${image}-{{ version }}.cdx.json"
    done

# ═════════════════════════════════════════════════════════════════════════════
# Repository hygiene
# ═════════════════════════════════════════════════════════════════════════════

# Lint GitHub Actions workflows (actionlint) and audit them for security issues (zizmor)
[group('repo')]
lint-workflows:
    {{ docker_tool }} {{ actionlint_image }} -color
    uvx zizmor@{{ zizmor_version }} .

# ═════════════════════════════════════════════════════════════════════════════
# Aggregates
# ═════════════════════════════════════════════════════════════════════════════

# Apply all formatters
[group('all')]
fmt: api-format worker-format web-format

# Run every linter and format check
[group('all')]
lint: api-lint worker-lint web-lint lint-workflows

# Run all unit tests
[group('all')]
test-unit: api-test-unit worker-test-unit web-test-unit

# Run unit, integration and end-to-end tests
[group('all')]
test: test-unit api-test-integration web-test-e2e

# Everything CI runs, in one go
[group('all')]
ci: lint test security
