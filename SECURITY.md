# Security Policy

## Supported Versions

| Version | Supported |
|---|---|
| 1.1.x | ✅ |
| 1.0.x | ❌ |

Only the latest minor release receives security fixes.

## Reporting a Vulnerability

Please do **not** report security vulnerabilities through public GitHub
issues, discussions, or pull requests.

Report them privately via one of:

- GitHub private vulnerability reporting:
  [Security Advisories](https://github.com/G6938/cnet/security/advisories/new)
- Email: mybothelp@gmail.com

Include as much of the following as you can:

- The affected package (`cnet` or `cnet.aspnetcore`) and version
- Type of issue (e.g. token leakage, request forgery, denial of service)
- Step-by-step reproduction or proof-of-concept code
- Impact assessment: what an attacker can achieve

## What to expect

- **Acknowledgement** within 72 hours.
- **Assessment and fix plan** within 7 days for confirmed issues.
- A fix is released as a patch version, the advisory is published, and you are
  credited unless you prefer to stay anonymous.

## Scope notes

Security-sensitive areas of this library include:

- Webhook secret-token validation (constant-time comparison)
- Bot token handling (never logged, never serialized)
- Replay protection for incoming updates
- Rate limiting and flood protection

Vulnerabilities in the Telegram Bot API itself or in the `Telegram.Bot`
dependency should be reported to those projects; we will track and adopt
upstream fixes.
