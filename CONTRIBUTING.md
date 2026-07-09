# Contributing to cnet

Thank you for considering a contribution. This document explains how to set up
a development environment, the standards the codebase follows, and how changes
get merged.

## Prerequisites

- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Git

## Getting started

```bash
git clone https://github.com/G6938/cnet.git
cd cnet
dotnet build
dotnet test
```

The solution has three parts:

| Project | Purpose |
|---|---|
| `src/Cnet` | Core library: client, routing, pipeline, sessions, throttling, polling |
| `src/Cnet.AspNetCore` | ASP.NET Core webhook integration |
| `tests/*` | Unit and integration tests |

## Development guidelines

- **Warnings are errors.** The build runs with `TreatWarningsAsErrors` and the
  .NET analyzers at `latest-recommended`. A change that introduces a warning
  does not build.
- **Everything is asynchronous.** No `.Result`, `.Wait()`, or `Thread.Sleep`
  in library code.
- **No comments in code.** The codebase is intentionally comment-free; write
  code that explains itself through naming and structure.
- **Public API changes need tests.** Every bug fix needs a regression test
  that fails without the fix.
- **Logging** uses source-generated `[LoggerMessage]` methods only.
- **No new dependencies** in `src/Cnet` without prior discussion in an issue.
  The core intentionally depends only on `Telegram.Bot` and
  `Microsoft.Extensions.*` abstractions.

## Running the test suite

```bash
dotnet test
```

If your machine only has a newer .NET runtime installed:

```bash
DOTNET_ROLL_FORWARD=Major dotnet test
```

## Submitting changes

1. Fork the repository and create a branch from `main`:
   `git checkout -b fix/short-description`
2. Make your change, including tests.
3. Make sure `dotnet build` and `dotnet test` pass locally.
4. Open a pull request against `main` using the pull request template.
5. A maintainer will review it. Small, focused pull requests get reviewed
   fastest — split unrelated changes into separate pull requests.

### Commit messages

Use the imperative mood and keep the subject under 72 characters:

```
Fix overflow in outbound throttle first-send path
```

Reference issues in the body when applicable (`Fixes #12`).

## Reporting bugs and requesting features

Use the [issue templates](https://github.com/G6938/cnet/issues/new/choose).
Bug reports need a minimal reproduction — a failing test or a short program.

## Security issues

Do **not** open a public issue for security vulnerabilities. See
[SECURITY.md](SECURITY.md) for the private disclosure process.

## License

By contributing, you agree that your contributions will be licensed under the
[MIT License](LICENSE).
