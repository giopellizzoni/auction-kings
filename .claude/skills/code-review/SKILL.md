---
name: code-review
description: Comprehensive code review following AuctionKings standards for .NET projects. Use when reviewing pull requests, auditing code quality, or preparing code for production deployment.
---

# AuctionKings .NET Code Review

Use this skill as the review rulebook for AuctionKings.
If a review needs an owner that inspects code and writes findings, the `code-reviewer` agent should own the task and this skill should supply the checklist.

## Review Order

1. Correctness and regressions
2. Security
3. Architecture boundaries
4. Performance
5. Style only after higher-signal issues

## Review Rules

- Enforce `API -> Application -> Domain <- Infrastructure`
- Prefer findings with file and line references
- Use `references/SECURITY.md`, `references/PERFORMANCE.md`, and `references/STYLE.md` only when deeper guidance is needed
- Do not pad reviews with generic advice; focus on actionable issues

## Feedback Format

1. Severity: Critical / High / Medium / Low
2. Location: file path and line
3. Problem: what is wrong and why it matters
4. Fix: concrete remediation
5. Reference: CWE / OWASP when relevant
