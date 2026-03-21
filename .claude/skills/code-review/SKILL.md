---
name: code-review
description: Comprehensive code review following AuctionKings standards for .NET projects. Use when reviewing pull requests, auditing code quality, or preparing code for production deployment.
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
3. **Security Review** — Check for vulnerabilities use [references/SECURITY.md](references/SECURITY.md) for guidelines
4. **Performance Review** — Identify bottlenecks and inefficiencies use [references/PERFORMANCE.md](references/PERFORMANCE.md) for guidelines
5. **Style Review** — Ensure consistency with `.editorconfig` and team standards use [references/STYLE.md](references/STYLE.md) for guidelines
6. **Build Validation** — Run build and tests for final check

## Architecture Review

Enforce Clean Architecture dependency direction: `API → Application → Domain ← Infrastructure`

- **Domain** must not reference Application, Infrastructure, or API layers
- **Application** must not reference Infrastructure or API layers
- **Infrastructure** must not reference API layer
- Verify CQRS separation: commands in `Commands/`, queries in `Queries/`
- Ensure domain events are published via Outbox Pattern, not directly
- Check that aggregates only expose behavior through methods, not public setters
