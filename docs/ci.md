# CI with GitHub Actions

## Principles

1. **The `justfile` is the API.** Every workflow installs its toolchain and then runs **one**
   `just` recipe. Anything CI does, you can run locally with the same command, and CI logic
   never hides in YAML.
2. **One workflow per concern per component.** Small workflows have focused triggers
   (`paths:` filters), fail independently, and show up as separate, readable checks on a PR.
3. **Least privilege, pinned everything.** Read-only tokens by default, actions pinned to commit
   SHAs, and tool images pinned to digests.

## Naming convention

Workflows are named **`Category · Component`**, and files mirror the name in kebab-case.
GitHub sorts the Actions sidebar alphabetically, so related workflows end up grouped:

```
E2E Tests · Web (Playwright)              e2e-tests-web.yml
Integration Tests · Jobs API              integration-tests-jobs-api.yml
Lint & Format · Jobs API                  lint-jobs-api.yml
Lint & Format · ML Worker                 lint-ml-worker.yml
Lint & Format · Web                       lint-web.yml
Release · Publish Images                  release-publish-images.yml
Release · Release Please                  release-please.yml
Repo · PR Title Check                     repo-pr-title.yml
Repo · Workflow Lint                      repo-workflow-lint.yml
Security · Container & IaC Scan (Trivy)   security-container-scan.yml
Security · Dependency Scan (Trivy)        security-dependency-scan.yml
Security · SAST (Semgrep)                 security-sast.yml
Security · Secret Scan (Gitleaks)         security-secret-scan.yml
Unit Tests · Jobs API                     unit-tests-jobs-api.yml
Unit Tests · ML Worker                    unit-tests-ml-worker.yml
Unit Tests · Web                          unit-tests-web.yml
```

Jobs and steps use plain-English sentence case (`Run unit tests`, `Publish test report`), so a
check reads like `Unit Tests · Jobs API / Run unit tests`.

## Workflow catalogue

| Workflow | Runs on | `just` recipe | What it guarantees |
|---|---|---|---|
| **Unit Tests · Jobs API** | PR/push touching `apps/jobs-api`, `contracts` | `api-test-unit` | xUnit tests incl. message-contract test; TRX + Cobertura |
| **Unit Tests · ML Worker** | `apps/ml-worker`, `contracts` | `worker-test-unit` | pytest with an 80 % coverage gate; contract test |
| **Unit Tests · Web** | `apps/web` (not `e2e/`) | `web-test-unit` | Vitest with coverage thresholds |
| **Integration Tests · Jobs API** | `apps/jobs-api`, `contracts` | `api-test-integration` | API over HTTP against real PostgreSQL + RabbitMQ (Testcontainers) |
| **E2E Tests · Web (Playwright)** | any `apps/**`, `deploy/compose`, `contracts` | `web-test-e2e` | builds all images, starts the compose stack, drives the UI end-to-end |
| **Lint & Format · Jobs API** | `apps/jobs-api` | `api-lint` | `dotnet format` clean, build with all analyzers as errors |
| **Lint & Format · ML Worker** | `apps/ml-worker` | `worker-lint` | ruff format + lint, strict mypy, `uv.lock` up to date |
| **Lint & Format · Web** | `apps/web` | `web-lint` | Prettier, ESLint (angular-eslint, no warnings) |
| **Security · Secret Scan (Gitleaks)** | every PR/push, weekly | `security-secrets` | no secrets anywhere in git history |
| **Security · SAST (Semgrep)** | `apps/**`, weekly | `security-sast` | no Semgrep findings in C#, Python, TS, Dockerfiles |
| **Security · Dependency Scan (Trivy)** | lockfiles, **daily** | `security-deps` | no fixable HIGH/CRITICAL CVEs in NuGet/uv/npm deps |
| **Security · Container & IaC Scan (Trivy)** | `apps/**`, `deploy/**`, **daily** | `security-images` | Dockerfiles follow best practices; no fixable HIGH/CRITICAL CVEs in images |
| **Release · Release Please** | push to `main` | – | maintains the release PR, cuts releases ([docs](release.md)) |
| **Release · Publish Images** | called on release / manual | `images-publish`, `images-sbom` | images in GHCR + SBOMs on the release |
| **Repo · PR Title Check** | every PR | – | PR title is a Conventional Commit (feeds release-please) |
| **Repo · Workflow Lint** | `.github/**`, `justfile` | `lint-workflows` | actionlint + zizmor security audit of all workflows |

Every workflow also has `workflow_dispatch`, so you can run it manually from the Actions tab.

## Conventions every workflow follows

| Convention | Why |
|---|---|
| `permissions: contents: read` at the top; jobs request more only when needed | A compromised step can't push code or tamper with releases |
| `actions/checkout` with `persist-credentials: false` | The token isn't left in `.git/config` for later steps |
| Third-party actions pinned to a **commit SHA** with a `# vX.Y.Z` comment | Tags are mutable, SHAs aren't. Dependabot updates both |
| `concurrency` group per workflow + PR; PR runs cancel superseded runs, `main` runs never do | Saves minutes and avoids racing deploys |
| `timeout-minutes` on every job | A hung job fails in minutes instead of burning 6 hours |
| Explicit `ubuntu-24.04` runner | `ubuntu-latest` moves under you |
| Toolchain setup in composite actions (`.github/actions/setup-*`) | Unit, integration and lint workflows of a component share one definition |
| Reports go to `artifacts/` and are uploaded; tests publish a job summary | Failures are diagnosable without re-running |
| Untrusted values (`inputs.*`, step outputs) are passed to `run:` via `env:` | Prevents script injection (zizmor `template-injection`) |
| Scheduled scans use odd minutes (`41 4 * * *`) | Avoids the top-of-hour scheduling rush on GitHub's side |

`Repo · Workflow Lint` enforces most of this (actionlint for correctness, zizmor for security).
The one exception is documented in [`.github/zizmor.yml`](../.github/zizmor.yml).

## Path filters and required status checks

Workflow-level `paths:` filters keep PRs fast: a docs-only change runs only the secret scan and
the PR title check. The trade-off: **a workflow skipped by its path filter never reports a
status**. If you mark it *required* in branch protection, PRs that don't touch that path wait
forever.

Options, from simplest to most robust:

1. Require only the always-running checks (`Security · Secret Scan`, `Repo · PR Title Check`) and
   rely on review discipline for the rest.
2. Require the path-filtered checks and accept that unrelated PRs need an admin merge.
3. Replace workflow-level `paths:` with a first job that uses `dorny/paths-filter` and skips
   the test job conditionally. A *skipped job* counts as passing for required checks.

> **Private repositories on GitHub Free cannot use branch protection or rulesets.** You need
> GitHub Pro (personal) or Team (organisation) for required checks.

## Private-repository considerations

| Topic | Impact | What this repo does |
|---|---|---|
| Actions minutes | Free: 2,000 min/month (Pro 3,000, Team 3,000) for private repos | path filters, cancel-in-progress, dependency caches, Trivy DB cache |
| GitHub Advanced Security | CodeQL, code-scanning SARIF upload, dependency review and secret-scanning push protection are paid | free tools (Gitleaks, Semgrep CE, Trivy); reports as artifacts + failing checks |
| Artifact attestations (`actions/attest-build-provenance`) | need GitHub Enterprise Cloud for private repos | BuildKit SBOM + provenance attestations on the images, plus SBOMs on the release |
| GHCR | private packages count against the plan's storage quota | 4 small images per release |
| Dependabot | alerts, security updates and version updates are free | configured in `.github/dependabot.yml` |

## Running CI locally

```bash
just lint          # every linter / formatter check
just test-unit     # all unit tests
just test          # unit + integration + e2e (needs Docker)
just security      # gitleaks, semgrep, trivy deps + images (needs Docker)
just ci            # all of the above
just hooks-run     # every pre-commit hook on all files
```

## Adding a workflow

1. Add (or reuse) a `just` recipe, and make it work locally first.
2. Copy the closest existing workflow. Keep the name format `Category · Component` and match
   the file name to it.
3. Keep `permissions` minimal, pin new actions to a SHA (`git ls-remote https://github.com/OWNER/REPO refs/tags/vX.Y.Z`).
4. Add the workflow file and its composite action to its own `paths:` filter.
5. Run `just lint-workflows`, then add the workflow to the table above.

## Dependency updates

[`.github/dependabot.yml`](../.github/dependabot.yml) opens weekly, grouped PRs for NuGet, uv,
npm, Dockerfiles, compose images and GitHub Actions. Two settings shape them:

- A **7-day cooldown**, so a release must be public for a week before Dependabot proposes it.
  This shields the repo from most compromised-package incidents.
- **Prefixes** that drive release-please. Runtime dependencies use `deps:` (patch release).
  Dev dependencies use `chore(deps-dev):` and GitHub Actions use `ci(deps):`. Neither
  triggers a release.

Tool versions inside the `justfile` (Gitleaks, Trivy, Semgrep, actionlint, zizmor) and the
pre-commit hook revisions are bumped by hand. Run `pre-commit autoupdate` and update the
pinned image digests, then run `just security lint-workflows`.
