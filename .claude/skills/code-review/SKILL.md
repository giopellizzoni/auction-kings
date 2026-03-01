---
name: code-review
description: Comprehensive code review following AuctionKings standards for .NET projects. Use when reviewing pull requests, auditing code quality, or preparing code for production deployment.
compatibility:
  - claude-opus-4-6
  - claude-sonnet-4-6
---

# AuctionKings .NET Code Review

This skill guides comprehensive code reviews covering security, performance, and style standards for ASP.NET Core 10 / Clean Architecture projects.

## When to Use This Skill

- Reviewing pull requests before merge
- Auditing existing code for quality issues
- Preparing features for production deployment
- Onboarding reviews for new team members' code

## Review Workflow

1. **Architecture Review** — Validate Clean Architecture dependency rules
2. **Domain Review** — Check DDD patterns and aggregate correctness
3. **Security Review** — Check for vulnerabilities
4. **Performance Review** — Identify bottlenecks and inefficiencies
5. **Style Review** — Ensure consistency with `.editorconfig` and team standards
6. **Build Validation** — Run build and tests for final check

## Architecture Review

Enforce Clean Architecture dependency direction: `API → Application → Domain ← Infrastructure`

- **Domain** must not reference Application, Infrastructure, or API layers
- **Application** must not reference Infrastructure or API layers
- **Infrastructure** must not reference API layer
- Verify CQRS separation: commands in `Commands/`, queries in `Queries/`
- Ensure domain events are published via Outbox Pattern, not directly
- Check that aggregates only expose behavior through methods, not public setters

Run architecture tests to validate:
