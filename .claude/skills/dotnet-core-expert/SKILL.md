---
name: dotnet-core-expert
description: Defines standards and patterns for building .NET Core applications with cloud-native architecture, high-performance microservices, modern C# patterns, and advanced ASP.NET Core features. Use when implementing features, reviewing architecture decisions, or ensuring code meets AuctionKings quality standards.
---

# AuctionKings .NET Core Expert

This skill defines the standards, patterns, and workflows for implementing features in the AuctionKings platform using .NET 10 and modern C# practices.

## Standards

- .NET 10 and C# 14 features used appropriately
- Nullable reference types enabled and respected
- Test coverage > 80%
- OpenAPI documented
- No warnings suppressed — fix root cause

## Modern C# Patterns

- Record types for immutable DTOs and value objects
- Pattern matching over type checks
- Global usings and file-scoped namespaces
- Init-only properties and required members
- Primary constructors for DI

## Clean Architecture

- Dependency direction: `API → Application → Domain ← Infrastructure`
- CQRS with MediatR: commands in `Commands/`, queries in `Queries/`
- Repository + Unit of Work pattern
- Constructor-only dependency injection
- No business logic in controllers

## ASP.NET Core

- Middleware for cross-cutting concerns (exception handling, correlation IDs, rate limiting)
- Filters/attributes for reusable endpoint behavior
- Model binding with explicit `[FromRoute]`, `[FromQuery]`, `[FromBody]`
- Fully async pipeline — no `.Result` / `.Wait()`
- `CancellationToken` propagated throughout

## Entity Framework Core

- Code-first with explicit configurations (`IEntityTypeConfiguration<T>`)
- `AsNoTracking()` for all read-only queries
- Eager loading with `Include` — no lazy loading
- Project to DTOs with `Select` — never load full entities for reads
- Single `SaveChangesAsync()` call per unit of work

## Testing

- xUnit with `WebApplicationFactory` for integration tests
- Testcontainers for PostgreSQL in integration tests (`PostgreSqlFixture`)
- No mocking the database — integration tests hit real containers
- Arrange / Act / Assert structure

## Performance

- Async I/O throughout
- `StringBuilder` for string concatenation in loops
- Generic collections — avoid boxing value types
- `ConfigureAwait(false)` in library/infrastructure code
- Connection pooling configured for PostgreSQL

## Code Excellence Checklist

- [ ] Curly braces on all `if`/`else` blocks
- [ ] Nullable reference types handled
- [ ] Async methods suffixed with `Async` and accept `CancellationToken`
- [ ] No access modifiers on interface members
- [ ] Expression-bodied members where appropriate
- [ ] No static state unless absolutely necessary
- [ ] Resources disposed with `using` declarations
- [ ] Treat warnings as errors — no `#pragma warning disable` without justification

## Task Tracking

All implementation tasks are documented in `tasks/TaskNN.md`. When implementing a task:

1. **Before starting** — read the relevant `tasks/TaskNN.md` to understand scope, acceptance criteria, and dependencies
2. **Check current status** — the `## Status` section shows ✅ Completed / 🔄 In Progress / ⏳ Not Started
3. **After completing** — update the task file:
   - Change `## Status` to `✅ **Completed**` with a brief summary of what was implemented
   - Update the `## GitHub Issues` table: mark resolved issues as `✅ closed`
   - Update `README.md` task progress table if the status changed
4. **Close GitHub issues** — for any issue whose acceptance criteria is now met, close it via `gh issue close <number> --repo PellizzoniCode/auction-kings`
