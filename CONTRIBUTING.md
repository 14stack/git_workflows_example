# Contributing

## Workflow

1. Branch from `main`.
2. Run `just bootstrap` once. It installs the git hooks, which check formatting and run the
   unit tests of the apps you touch on every commit.
3. Commit using [Conventional Commits](https://www.conventionalcommits.org/). The commit-msg hook enforces it.
4. Open a PR whose **title** is a Conventional Commit (`feat(api): add job priority`). PRs are
   **squash-merged**, so the title is what release-please reads (see [docs/release.md](docs/release.md)).
5. CI must be green. Every check maps to a `just` recipe you can run locally ([docs/ci.md](docs/ci.md)).

## Commit / PR title format

```
<type>(<optional scope>)<optional !>: <lowercase subject>
```

| Type | Use for | Releases? |
|---|---|---|
| `feat` | new user-facing behaviour | minor |
| `fix` | bug fixes | patch |
| `perf` | performance improvements | patch |
| `deps` | runtime dependency updates | patch |
| `revert` | reverting a previous commit | patch |
| `docs`, `refactor`, `test`, `build`, `ci`, `chore` | everything else | no |

Scopes: `api`, `worker`, `web`, `contracts`, `deploy`, `deps`, `deps-dev`, `ci`, `release`,
`security`, `docs`. Add `!` (or a `BREAKING CHANGE:` footer) for breaking changes.

## Before pushing

```bash
just fmt         # apply formatters
just lint        # what CI lints
just test-unit   # fast tests; `just test` for integration + e2e
```

## Changing the message contract

Update [`contracts/`](contracts) first. Both the API's and the worker's contract tests read
it, so whichever side is out of date fails its unit tests.
