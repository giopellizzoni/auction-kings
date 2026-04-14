---
name: dotnet-core-expert
description: Defines standards and patterns for building .NET Core applications with cloud-native architecture, high-performance microservices, modern C# patterns, and advanced ASP.NET Core features. Use when implementing features, reviewing architecture decisions, or ensuring code meets AuctionKings quality standards.
---

# AuctionKings .NET Core Expert

Use this skill as the repo-specific rulebook for .NET implementation in AuctionKings.
If the task needs execution ownership across multiple steps, the `dotnet-core-expert` agent should own the task and this skill should supply constraints.

## Core Rules

- Keep dependency flow `API -> Application -> Domain <- Infrastructure`
- Keep controllers thin; put business rules in Application/Domain
- Use async end-to-end and pass `CancellationToken`
- Respect nullable reference types and avoid warning suppression
- Prefer explicit model binding and validation for request DTOs
- Use `AsNoTracking()` for read queries and one `SaveChangesAsync()` per unit of work
- Prefer modern C# features only when they improve clarity

## Project Expectations

- CQRS structure: commands in `Commands/`, queries in `Queries/`
- Explicit EF Core configurations; no lazy loading
- Read models should project only required fields
- Tests should cover changed behavior; prefer existing integration-test patterns over mocks for persistence-heavy code

## Task Tracking

- Read the relevant `tasks/TaskNN.md` before implementation when one exists
- Update task status after finishing
- Close linked GitHub issues only when the user asked for issue sync
