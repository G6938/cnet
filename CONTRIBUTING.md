# Contributing to cnet

Thanks for your interest in improving cnet. This guide covers everything you
need to get set up, make a change, and open a pull request.

## Quick start

```bash
git clone https://github.com/G6938/cnet.git
cd cnet
dotnet build
dotnet test
```

You need the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

> [!NOTE]
> Tests live in a local-only solution. Use `cnet.local.slnx` to build and run
> them; the tracked `cnet.slnx` contains only the shippable packages.

## Project layout

| Path | What it is |
|------|------------|
| `src/Cnet` | Core toolkit: client, routing, pipeline, sessions, polling |
| `src/Cnet.AspNetCore` | ASP.NET Core webhook endpoint |
| `src/Cnet.Redis` | Durable queue, distributed throttle, replay guard, sessions, albums |
| `src/Cnet.Metrics` | OpenTelemetry-compatible metrics |
| `src/Cnet.Testing` | Test harness with a fake bot client |
| `samples/EchoBot` | A complete runnable example bot |
| `tests/` | Unit and integration tests |

## How to contribute

1. **Open an issue first** for anything non-trivial, so we can agree on the
   approach before you write code.
2. **Fork and branch** from `main`: `git checkout -b fix/short-description`.
3. **Make your change**, with tests.
4. **Run the checks** below until they pass.
5. **Open a pull request** against `main` using the template. Keep it focused —
   one change per pull request gets reviewed fastest.

## Before you push

```bash
dotnet build              # must succeed with zero warnings
dotnet test cnet.local.slnx
```

> [!IMPORTANT]
> The build treats warnings as errors and runs the .NET analyzers. A change
> that introduces a warning will not build.

## Coding standards

- **Everything is async.** No `.Result`, `.Wait()`, or `Thread.Sleep` in
  library code.
- **No comments.** The codebase is intentionally comment-free — write code that
  explains itself through naming and structure.
- **Tests are required.** Every bug fix needs a regression test that fails
  without the fix; every feature needs coverage.
- **Logging** uses source-generated `[LoggerMessage]` methods only.
- **No new dependencies** in `src/Cnet` without discussing it in an issue first.
  The core depends only on `Telegram.Bot` and the `Microsoft.Extensions.*`
  abstractions.

## Commit messages

Use the imperative mood and keep the subject under 72 characters. Reference the
issue in the body when there is one.

```
Fix overflow in outbound throttle first-send path

Fixes #12.
```

> [!WARNING]
> Never commit a bot token, secret, or `.env` file. If you leak a token, revoke
> it in @BotFather immediately.

## Security issues

Do not open a public issue for a vulnerability. Follow the
[security policy](SECURITY.md) to report it privately.

## License

By contributing, you agree that your contributions are licensed under the
[MIT License](LICENSE).
