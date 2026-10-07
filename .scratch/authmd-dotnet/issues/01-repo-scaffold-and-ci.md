# 01: Repo & solution scaffold + CI

**What to build:** A buildable, testable, packable solution skeleton for the `authmd-dotnet` monorepo so every later ticket lands into a green pipeline. Two class libraries (`AuthMd.AspNetCore`, `AuthMd.Client`), a test project, and empty sample app shells, all net10.0, under one solution, with an MIT license and GitHub Actions CI.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [x] Solution builds with `AuthMd.AspNetCore`, `AuthMd.Client`, a test project, and sample app shells — all net10.0
- [x] MIT `LICENSE` file present at the repo root
- [ ] GitHub Actions workflow builds, tests, and packs on every push, and passes with one trivial test
- [x] `dotnet pack` produces a `.nupkg` for both libraries

## Comments

- 2026-10-06: Scaffold in place (`AuthMd.slnx`, `src/`, `tests/`, `samples/`, `LICENSE`, `.github/workflows/ci.yml`). The workflow's restore/build/test/pack steps pass when run locally in Release with one smoke test (sample Service boots and answers `GET /` via `WebApplicationFactory`). The CI box stays open until the repo is pushed and the workflow has actually run on GitHub — nothing has been pushed and no git repo has been initialised yet.
