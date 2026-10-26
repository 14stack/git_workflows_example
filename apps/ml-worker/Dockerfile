# syntax=docker/dockerfile:1
# Base images are pinned by tag + digest (reproducible builds); Dependabot bumps both.

# ---------------------------------------------------------------------------
FROM python:3.13-slim@sha256:d98cc29d3266e535dd9da1099688d9cb3ca4753813571e071763d17725460962 AS build
# Pinned uv; installed from PyPI so the build only needs one package index.
RUN pip install --no-cache-dir uv==0.11.32
ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy \
    UV_PYTHON_DOWNLOADS=never \
    UV_PROJECT_ENVIRONMENT=/app/.venv
WORKDIR /src

# Dependencies first (cached layer), then the project itself.
COPY pyproject.toml uv.lock .python-version README.md ./
RUN uv sync --locked --no-dev --no-install-project
COPY src/ src/
RUN uv sync --locked --no-dev --no-editable

# ---------------------------------------------------------------------------
FROM python:3.13-slim@sha256:d98cc29d3266e535dd9da1099688d9cb3ca4753813571e071763d17725460962 AS runtime
# pip is not needed at runtime and its vendored libraries are a recurring source of CVEs.
RUN python -m pip uninstall --yes --quiet pip \
 && groupadd --system --gid 10001 worker \
 && useradd --system --uid 10001 --gid worker --no-create-home --shell /usr/sbin/nologin worker
COPY --from=build /app/.venv /app/.venv
ENV PATH="/app/.venv/bin:${PATH}" \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1
USER 10001
ENTRYPOINT ["ml-worker"]
