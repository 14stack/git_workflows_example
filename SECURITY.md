# Security policy

## Reporting a vulnerability

Please **do not open a public issue** for security problems.

Report it privately to the repository maintainers (listed in
[`.github/CODEOWNERS`](.github/CODEOWNERS)), either through a GitHub security advisory draft
(*Security → Advisories → New draft advisory*) or by direct message. Please include:

- the affected component (`jobs-api`, `ml-worker`, `web`, images, workflows)
- steps to reproduce or a proof of concept
- the impact you expect

We aim to acknowledge reports within 3 working days and to ship a fix through the normal
[release process](docs/release.md) as a `fix:` release.

## Supported versions

Only the latest release receives security fixes.

## Automated checks

Every change is scanned for secrets, code issues, vulnerable dependencies and container
vulnerabilities. See [docs/security.md](docs/security.md).
