# Security checks

All security tooling is **free and open source** (no GitHub Advanced Security, no SaaS
licences). Each tool runs from a pinned container image (tag **and** digest) via the
`justfile`, so local runs and CI behave identically.

| Concern | Tool | Recipe | Workflow | When | Fails on |
|---|---|---|---|---|---|
| Secrets in git history | [Gitleaks](https://github.com/gitleaks/gitleaks) (MIT) | `just security-secrets` | Security · Secret Scan (Gitleaks) | every PR/push, weekly | any finding |
| Bugs in **our** code (SAST) | [Semgrep CE](https://github.com/semgrep/semgrep) (LGPL) | `just security-sast` | Security · SAST (Semgrep) | code changes, weekly | any finding |
| Vulnerable **dependencies** (SCA) | [Trivy](https://github.com/aquasecurity/trivy) (Apache-2.0) | `just security-deps` | Security · Dependency Scan (Trivy) | lockfile changes, **daily** | fixable HIGH/CRITICAL |
| Vulnerable / misconfigured **containers** | Trivy | `just security-images` | Security · Container & IaC Scan (Trivy) | image changes, **daily** | fixable HIGH/CRITICAL, Dockerfile HIGH/CRITICAL |
| Insecure **workflows** | [zizmor](https://github.com/zizmorcore/zizmor) (MIT) + actionlint | `just lint-workflows` | Repo · Workflow Lint | `.github/**` changes | any finding |
| Secrets **before** they are committed | Gitleaks pre-commit hook | `just hooks-install` | – | every local commit | any finding |

Why three tools and not one? Each covers a different risk, and each already covers all three
languages. Trivy handles dependencies, images and Dockerfiles but does not analyse our own
source code, so Semgrep fills that gap. Gitleaks is the specialist for secrets and also runs
as a pre-commit hook.

## Coverage details

- **Gitleaks** scans the full history (`fetch-depth: 0`), so a secret that was committed
  and later deleted is still reported. Rotate it, then rewrite history.
- **Semgrep** runs the `p/default`, `p/csharp`, `p/python`, `p/typescript` and `p/dockerfile`
  rulesets with metrics disabled. The registry rules are free to use (Semgrep Rules License).
  [Opengrep](https://github.com/opengrep/opengrep) is a drop-in fork if you prefer fully open rules.
- **NuGet audit** also runs on every `dotnet restore` (`<NuGetAudit>` in
  `Directory.Build.props`). Warnings are errors, so a newly disclosed vulnerable package
  fails the .NET build before the nightly Trivy scan runs.
- **Trivy (dependencies)** reads `packages.lock.json` and `Directory.Packages.props` (NuGet),
  `uv.lock` and `package-lock.json`. Development-only dependencies are excluded by default;
  add `--include-dev-deps` to the recipe to include them.
- **Trivy (images)** builds every image with `docker buildx bake`, saves it as a tarball (no
  Docker socket needed), and scans OS packages plus language libraries. `trivy config` checks
  the Dockerfiles: running as root, missing `USER`, `:latest` tags, and so on.

## Why not CodeQL / GitHub code scanning?

This repository is **private**. On private repositories CodeQL, SARIF upload to the Security
tab, dependency review and push protection all require paid GitHub Advanced Security. The
reports (SARIF / JSON) are uploaded as **workflow artifacts** instead, and the job fails on
findings. If the repository becomes public, or you buy GHAS, add `github/codeql-action` and
upload the SARIF files the recipes already produce.

## Container hardening already in place

- **jobs-api / migrator**: Ubuntu *chiseled* (distroless) .NET images: no shell, no package
  manager, non-root `$APP_UID`.
- **ml-worker**: `python:3.13-slim`, a dedicated non-root user (UID 10001), `pip` removed from
  the runtime image, dependencies installed from the lockfile only (`uv sync --locked`).
- **web**: `nginx-unprivileged` (UID 101, port 8080), Alpine packages upgraded at build time,
  and strict security headers (CSP, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`)
  on every response via [`apps/web/nginx/security-headers.conf`](../apps/web/nginx/security-headers.conf).
- Released images carry **SBOM and SLSA provenance attestations** (BuildKit), and CycloneDX SBOMs
  are attached to each GitHub Release.

## Supply-chain hygiene

- GitHub Actions are pinned to commit SHAs; Dependabot updates them weekly.
- Dockerfile base images are pinned by tag **and** digest (`python:3.13-slim@sha256:…`), so
  builds are reproducible. Dependabot bumps the digest, and the nightly container scan flags
  when a bump is due.
- Security tool images are pinned by digest in the `justfile`.
- Dependabot waits **7 days** before proposing a new release (`cooldown`), which avoids
  most "malicious version published and pulled immediately" incidents.
- Lockfiles are enforced everywhere: `--locked-mode` (NuGet in CI), `uv sync --locked`, `npm ci`.

## Handling findings

Fix first (upgrade the package or base image), suppress second. Every suppression needs a
reason, and ideally an expiry date.

| Tool | Suppress a single finding | Ignore paths |
|---|---|---|
| Gitleaks | `# gitleaks:allow` comment on the line, or its fingerprint in `.gitleaksignore` | `[[allowlists]]` in [`.gitleaks.toml`](../.gitleaks.toml) |
| Semgrep | `# nosemgrep: <rule-id>` comment with a justification | [`.semgrepignore`](../.semgrepignore) |
| Trivy | entry with `statement` + `expired_at` in [`.trivyignore.yaml`](../.trivyignore.yaml) | `--skip-dirs` in the recipe |
| zizmor | `# zizmor: ignore[<audit>]` comment | `rules.<audit>` in [`.github/zizmor.yml`](../.github/zizmor.yml) |

## Running the scans locally

```bash
just security-secrets   # seconds
just security-sast      # ~1 min (downloads rules)
just security-deps      # ~30 s (first run downloads the vulnerability DB to ~/.cache/trivy)
just security-images    # builds all images first; several minutes
just security           # all of the above
```

Behind a TLS-intercepting corporate proxy, the tool containers need your CA bundle:

```bash
export TOOL_DOCKER_ARGS="-v /etc/ssl/certs/ca-certificates.crt:/etc/ssl/certs/ca-certificates.crt:ro"
```

## Reporting a vulnerability

See [SECURITY.md](../SECURITY.md).
