# Security Policy

## Supported Versions

Security fixes are released for the latest minor version. Older minor versions
do not receive backported fixes; upgrade to the latest release to stay
supported.

| Version | Supported |
|---|---|
| 1.6.x | ✅ |
| < 1.6 | ❌ |

This policy covers all packages in the project: `cnet`, `cnet.aspnetcore`,
`cnet.redis`, and `cnet.metrics`.

## Reporting a Vulnerability

Do not report security vulnerabilities through public GitHub issues,
discussions, or pull requests. Public disclosure before a fix is available puts
every user at risk.

Report privately through either channel:

- **GitHub Security Advisories** (preferred):
  <https://github.com/G6938/cnet/security/advisories/new>
- **Email:** mybothelp@gmail.com

To help us triage quickly, include as much as you can:

- Affected package and version
- A description of the vulnerability and its impact
- Steps to reproduce, or a minimal proof of concept
- Any known mitigations or workarounds

Never include a live bot token, production secret, or personal data in a
report. Redact them or share a reproduction that does not require them.

## Our Commitment

- We acknowledge every report within **72 hours**.
- We provide an initial assessment, including severity and a remediation plan,
  within **7 days**.
- We keep you informed through to the fix, coordinate a disclosure timeline,
  and credit you in the advisory unless you ask to remain anonymous.

## Security-Relevant Areas

The following parts of the library carry the highest security weight. Reports
touching them are prioritized:

- **Webhook authentication** — the secret token is compared in constant time to
  prevent timing attacks and forged updates.
- **Bot token handling** — tokens are read from configuration and are never
  logged or serialized.
- **Replay protection** — each update is processed once, including across
  multiple instances when the Redis package is used.
- **Rate limiting and flood control** — inbound and outbound limits guard
  against abuse and Telegram API bans.

## Scope

This policy applies to the cnet packages themselves. Vulnerabilities in the
Telegram Bot API or in upstream dependencies such as `Telegram.Bot` or
`StackExchange.Redis` should be reported to those projects; we track and adopt
their fixes as they are released.
