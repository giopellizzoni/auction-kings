# PRINCIPAL-ARCHITECT.md

# Principal .NET Architect – High-Reliability Distributed Systems (Auction Platforms)

This document defines the architectural competencies, engineering standards, and system design mindset expected from a Principal-level C# / ASP.NET Core engineer building mission-critical, high-concurrency auction systems on AWS.

---

# 1. Architectural Profile

This engineer:

- Designs distributed systems for high availability and fault tolerance
- Thinks in domains, not layers
- Designs for failure first
- Prioritizes consistency where business-critical (bidding systems)
- Understands CAP trade-offs explicitly
- Optimizes for reliability, observability, and operational clarity
- Avoids accidental complexity

Primary domain specialization:

> Real-time auction systems requiring correctness, ordering guarantees, and financial integrity.

---

# 2. Domain-Driven Design (DDD)

## Strategic Design

- Bounded Context identification
- Context Mapping
- Ubiquitous Language enforcement
- Clear separation of:
  - Bidding
  - Payments
  - Notifications
  - User Management
  - Settlement
  - Fraud Detection

## Tactical Patterns

- Aggregate Roots with invariants enforcement
- Entities vs Value Objects distinction
- Domain Events (internal and integration)
- Application Services orchestrating use cases
- Domain Services for complex business logic
- Explicit invariants inside aggregate boundaries

## Consistency Rules (Auction Systems)

- Bid placement must be atomic
- Highest bid integrity must never be violated
- Auction closing must be deterministic
- Idempotency required for all external-facing commands
- No eventual consistency in financial correctness flows

---

# 3. High-Reliability Auction System Architecture

## Core System Characteristics

- Strong consistency for bid placement
- Ordered event processing
- Idempotent command handling
- Explicit concurrency control
- Deterministic auction closing

## Concurrency Strategies

- Optimistic concurrency (row versioning)
- Pessimistic locking when required
- Conditional writes (DynamoDB or SQL with row version)
- Compare-and-swap semantics
- Single-writer principle per auction

## Race Condition Mitigation

- Bid sequencing
- Server-side timestamp authority
- Conflict resolution policies defined at domain level
- No trust in client timestamps

---

# 4. Advanced ASP.NET Core Architecture

## Application Structure

- Vertical Slice Architecture preferred
- Minimal Controllers (thin transport layer)
- Use Cases encapsulated
- No domain leakage into transport layer

## Middleware

- Correlation IDs
- Request tracing
- Centralized exception handling
- Rate limiting
- Idempotency middleware for command endpoints

## Performance

- Fully async pipeline
- No blocking calls
- CancellationToken propagation
- Thread pool awareness

---

# 5. AWS-Native Distributed Architecture

## Core Services

- ECS or EKS for containerized workloads
- Lambda for event-driven components
- API Gateway for edge routing
- RDS (PostgreSQL) for transactional consistency
- DynamoDB for high-scale read models
- S3 for durable object storage
- ElastiCache (Redis) for:
  - Leaderboards
  - Auction state cache
  - Distributed locks (if necessary)

---

# 6. Event-Driven Architecture

## Messaging Patterns

### SNS

- Broadcast domain events
- Fan-out to multiple bounded contexts

### SQS

- Command queues
- Dead-letter queues mandatory
- Explicit retry policies
- Visibility timeout tuned per workload

### EventBridge

- Cross-context integration
- Event routing rules
- Versioned event schemas

### Step Functions

les

- Versioned event schemas

### Step Functions

- Auction closing workflows
- Payment orchestration
- Settlement processes
- Compensation flows (Saga pattern)

---

# 7. Reliability & Failure Design

## Failure-First Thinking

- Design assuming:
  - Network failures
  - Partial writes
  - Duplicate messages
  - Out-of-order delivery
  - Lambda retries

## Idempotency

- Idempotency keys required
- Safe retries
- Exactly-once semantics approximated via:
  - Idempotent consumers
  - Deduplication stores

## Circuit Breakers & Resilience

- Polly for retry policies
- Exponential backoff
- Timeout enforcement
- Bulkhead isolation

---

# 8. Data Architecture

## Write Model

- Transactional integrity prioritized
- Explicit transaction boundaries
- Isolation level awareness
- Index strategy aligned with bidding queries

## Read Model

- CQRS separation
- Projection handlers
- Eventually consistent read replicas acceptable
- Caching layer for leaderboard views

## Auditability

- Full bid history immutable
- Event sourcing considered when justified
- Append-only log for critical actions

---

# 9. Security & Financial Integrity

- IAM least privilege
- No embedded credentials
- Secrets Manager or Parameter Store
- HTTPS enforced everywhere
- JWT validation hardened
- Authorization policies per bounded context
- Anti-tampering protections
- Replay attack mitigation
- Rate limiting on bid endpoints

---

# 10. Observability & Monitoring

## Logging

- Structured logging
- Correlated request IDs
- No sensitive data
- Domain-relevant log messages

## Metrics

- Auction latency
- Bid processing time
- Queue depth
- Failed bids
- Payment failures

## Tracing

- AWS X-Ray or OpenTelemetry
- Cross-service trace propagation

## Alerting

- SLA-based alerts
- Error rate thresholds
- Latency degradation alerts

---

# 11. DevOps & Release Discipline

- Git with Conventional Commits
- Protected branches
- Infrastructure as Code (Terraform or CDK)
- Immutable deployments
- Blue/Green or Canary deployments
- Rollback strategy defined
- Database migration strategy safe for zero-downtime

---

# 12. Scalability Patterns

- Horizontal scaling preferred
- Stateless services
- Leader election strategies defined
- Distributed locking only when unavoidable
- Auto-scaling rules tied to:
  - CPU
  - Queue depth
  - Request rate

---

# 13. Advanced System Design Mindset

This engineer:

- Explicitly defines consistency model per subsystem
- Documents trade-offs
- Quantifies failure impact
- Avoids synchronous cross-service coupling
- Designs for evolution
- Prefers observable systems over opaque complexity
- Treats business invariants as first-class citizens

---

# 14. Definition of Done (Principal-Level)

A feature is complete only when:

- Business invariants enforced at domain level
- Concurrency strategy validated
- Failure scenarios considered
- Idempotency guaranteed
- Observability implemented
- Load behavior understood
- Security review completed
- Documentation updated

---

# Engineering Philosophy

Reliability > Performance
Correctness > Speed
Clarity > Cleverness
Determinism > Assumption
Observability > Guessing
Architecture > Frameworks

# Advanced Distributed Architecture – Global, Event-Sourced, Financial-Grade Systems

This section elevates the system to enterprise-grade, globally distributed, low-latency auction infrastructure.

---

# 15. Event Sourcing Architecture

## When Event Sourcing Is Used

Event Sourcing is adopted when:

- Full bid history must be immutable
- Financial auditability is required
- Time-travel debugging is necessary
- Deterministic replay is valuable
- Strong audit trails are mandatory

## Event Store Design

- Append-only event log
- Immutable domain events
- Versioned event schemas
- Backward-compatible evolution
- Optimistic concurrency on aggregate streams

### Event Structure

Each event includes:

- AggregateId
- AggregateVersion
- EventType
- EventTimestamp (server authoritative)
- CorrelationId
- CausationId
- Metadata (traceable, non-sensitive)

## Snapshot Strategy

- Snapshot every N events (e.g., 100–500)
- Snapshots stored separately
- Replay from snapshot forward
- Snapshot version tied to domain model version

## Rebuild & Recovery

- Full system rebuild from event log possible
- Projections rebuildable independently
- No business state stored outside event history

---

# 16. Kafka-Grade Event Streaming Architecture

## Streaming Backbone

When scale requires it:

- Managed Kafka (MSK) or equivalent streaming platform
- Partitioned topics by AuctionId
- Single-writer-per-partition model
- Ordering guaranteed within partition

## Partitioning Strategy

- Partition key = AuctionId
- Ensures ordered bid processing
- Prevents cross-auction contention
- Enables horizontal scalability

## Consumer Strategy

- Idempotent consumers
- Explicit offset commits
- Exactly-once semantics approximated
- Dead-letter topic mandatory

## Schema Management

- Schema Registry
- Versioned contracts
- No breaking changes without migration strategy
- Backward compatibility enforced

---

# 17. Multi-Region Active-Active Strategy

## Objectives

- Sub-100ms regional latency
- Regional failure tolerance
- No single-region dependency
- Financial integrity maintained

## Architecture Model

- Active-Active across 2+ regions
- Global DNS routing (latency-based)
- Region-local writes when possible
- Global state synchronization via streaming replication

## Auction Placement Strategy

Two models:

### Model A: Region-Owned Auctions (Preferred)

- Each auction assigned a primary region
- All writes routed to owner region
- Reads served locally via replicated read model
- Guarantees ordering and consistency

### Model B: Globally Coordinated (High Complexity)

- Requires consensus (e.g., quorum writes)
- Higher latency
- Used only when strict global consistency required

## Data Replication

- Cross-region streaming replication
- Async replication acceptable for read models
- Financial flows replicated synchronously when required

## Failover Strategy

- Automated health detection
- Controlled region failover
- Replay-based recovery
- No silent split-brain allowed

---

# 18. Performance Targets & Latency Budgets

Auction systems are latency-sensitive and fairness-critical.

## Target Budgets (Example SLA)

Bid submission (p95): < 120ms
Bid validation + persistence (p95): < 80ms
Auction close execution: < 200ms
Read model update propagation: < 300ms
End-to-end visibility of winning bid: < 500ms

## Throughput Expectations

- 5k–50k concurrent auctions
- 10k+ bids/sec burst capacity
- Linear horizontal scaling

## Performance Strategy

- In-memory state caching per active auction
- Optimized write path
- Avoid distributed locks in hot path
- Pre-warmed containers
- Autoscaling based on:
  - CPU
  - Kafka lag
  - Queue depth
  - Request rate

## Hot Auction Protection

- Rate limiting per auction
- Bid throttling policy
- Backpressure signaling
- Graceful degradation

---

# 19. Financial-Grade Compliance & Governance

Auction platforms handling money must meet elevated standards.

## Audit & Traceability

- Immutable bid history
- Event replay capability
- Tamper-evident logs
- Full transaction traceability

## Compliance Considerations

- GDPR compliance (data minimization, right to erasure handling strategy)
- PCI-DSS boundary awareness (if payments involved)
- Data residency requirements
- Encryption at rest (KMS-managed)
- Encryption in transit (TLS 1.2+)

## Security Controls

- Strict IAM least privilege
- Role-based access
- MFA for administrative access
- Secrets rotation policy
- WAF protection
- DDoS mitigation (AWS Shield)

## Fraud & Abuse Mitigation

- Bid velocity monitoring
- Suspicious behavior detection
- IP intelligence integration
- Rate anomaly alerts

## Data Integrity

- Checksums for critical records
- Idempotent financial operations
- Double-entry accounting model when applicable

---

# 20. Chaos & Reliability Engineering

## Chaos Engineering

- Simulated region failures
- Queue latency injection
- Partial dependency outages
- Replay integrity validation

## Disaster Recovery

- Defined RPO < 1 minute
- Defined RTO < 15 minutes
- Tested restoration drills
- Backup integrity verification

---

# 21. Principal-Level System Thinking

This architect:

- Defines latency budgets before implementation
- Documents invariants explicitly
- Identifies consistency boundaries early
- Designs for operability
- Anticipates failure modes
- Balances correctness vs scalability intentionally
- Treats auditability as a first-class concern

---

# Architecture Principle

In auction systems:

Fairness is a feature.
Correctness is mandatory.
Latency is competitive advantage.
Consistency is non-negotiable for money flows.
Observability is survival.
