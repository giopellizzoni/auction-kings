---
name: security-auditor
description: >
  Performs a comprehensive, codebase-grounded security audit of the AuctionKings ASP.NET Core 10 project.
  Covers five domains: (1) Backend — auth/authz, input validation, injection, IDOR, rate limiting, CORS,
  CSRF, JWT, error handling; (2) Auction domain — bid manipulation, state transition bypasses, race
  conditions, replay attacks, concurrency token bypass; (3) Infrastructure/Cloud — Docker secrets,
  PostgreSQL/Redis exposure, hardcoded credentials, AllowAdmin, connection string leakage;
  (4) DevOps — Dockerfile hygiene, exposed ports, NuGet dependency risks; (5) SignalR — hub authorization,
  connection flooding, unauthorized channel access. ALWAYS use this skill when the user asks for a security
  review, security audit, vulnerability check, "what are the security issues", "is this secure", or wants
  to harden the application before a release or cloud deployment. The skill reads actual source files
  and returns actionable findings with file:line references, severity, impact, and remediation — not
  generic checklists.
---

# Security Auditor — AuctionKings

Perform a codebase-grounded security audit. Read real files, produce real findings, and avoid generic checklists.

## Domains To Cover

1. Backend/API
   Files: `Program.cs`, controllers, filters, appsettings, DI setup
   Check: auth/authz coverage, middleware order, CORS, HTTPS/HSTS, security headers, rate limiting, exception handling, validation pipeline, host/header issues

2. Auction business logic
   Files: auction commands/queries, behaviors, `Auction.cs`, `Bid.cs`
   Check: IDOR/ownership checks, state transition bypasses, replay/idempotency, bid race conditions, self-bidding, currency/price validation, concurrency-token enforcement

3. Infrastructure/cloud
   Files: persistence, repositories, Redis, infra DI, docker compose, appsettings
   Check: secrets in source, weak connection settings, `AllowAdmin`, TLS/SSL, exposed ports, raw SQL injection risk, destructive cascade behavior

4. DevOps/build
   Files: Dockerfiles, compose, `Directory.Build.props`, `Directory.Packages.props`
   Check: root containers, multi-stage builds, insecure bindings, warnings-as-errors disabled, stale/vulnerable packages, secrets baked into images

5. SignalR
   Files: hubs and `Program.cs`
   Check: hub authorization, group scoping vs global broadcast, JWT transport for WebSockets, connection flood protection, unsafe state-changing hub methods

## Reporting Rules

- Every finding must cite a repo-relative file path and line when possible
- Stub or placeholder code is still a finding if it would ship insecurely
- Prefer business-impact wording over taxonomy-only wording
- Remediation must be concrete and code-oriented
- Do not report controls that are already implemented correctly

## Output Format

Produce:

`# Security Audit Report — AuctionKings`

- Date
- Executive Summary
- Findings grouped by `CRITICAL`, `HIGH`, `MEDIUM`, `LOW`
- For each finding: Location, Description, Impact, Remediation
- Positive Findings
- Production Readiness Checklist

Severity guidance:

- Critical: auth bypass, privilege escalation, direct breach
- High: meaningful exploit with authenticated or realistic access
- Medium: defense-in-depth gap or constrained exposure
- Low: hardening or future-risk issue
