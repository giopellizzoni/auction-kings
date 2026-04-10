# Backlog de Tarefas — AuctionKings

> Organizado em Épicos → Histórias → Subtarefas
> Prioridades: 🔴 Crítico | 🟠 Alto | 🟡 Médio | 🟢 Baixo
> Atualizado em: 2026-04-10

---

## ÉPICO 1 — Fundações: Shared + Domain (Base Classes) ✅ CONCLUÍDO

> **Implementado em:** Task 01 + Task 02

---

### HIS-01 — Implementar base classes do Shared ✅

- [x] `Shared/Common/Entity.cs` — igualdade por Id, GetHashCode
- [x] `Shared/Common/AggregateRoot.cs` — DomainEvents list, AddDomainEvent, ClearDomainEvents
- [x] `Shared/Common/IDomainEvent.cs` — interface com OccurredAt
- [x] `Shared/Common/Result.cs` — Result<T> + Error pattern
- [x] `Shared/Common/PagedResult.cs` — paginação com metadata
- [x] `Shared/Interfaces/IDateTimeProvider.cs` — interface + SystemDateTimeProvider
- [x] `Shared/Interfaces/IIdempotentCommand.cs` — interface
- [x] Remover `Class1.cs` placeholders de todos os projetos

---

### HIS-02 — Implementar Domain Layer ✅

- [x] `Domain/Enums/AuctionStatus.cs` — Pending, Active, Closed, Cancelled
- [x] `Domain/Enums/BidStatus.cs`
- [x] `Domain/ValueObjects/Money.cs`
- [x] `Domain/ValueObjects/AuctionDuration.cs`
- [x] `Domain/ValueObjects/BidIncrement.cs`
- [x] `Domain/Errors/DomainErrors.cs`
- [x] `Domain/Events/BidPlacedEvent.cs`
- [x] `Domain/Events/AuctionCreatedEvent.cs`
- [x] `Domain/Events/AuctionExtendedEvent.cs`
- [x] `Domain/Events/AuctionClosedEvent.cs`
- [x] `Domain/Events/WinnerDeterminedEvent.cs`
- [x] `Domain/Aggregates/Auction/Bid.cs`
- [x] `Domain/Aggregates/Auction/Auction.cs` — PlaceBid, Create, Close, auto-extend anti-sniping
- [ ] `Domain/Aggregates/Auction/AuctionSnapshot.cs` — ❌ Fase 2 (Event Sourcing)
- [ ] `Domain/Aggregates/User/User.cs` — ❌ não necessário (auth via Cognito)

---

## ÉPICO 2 — Infraestrutura: Persistência (EF Core + PostgreSQL) ✅ CONCLUÍDO

> **Implementado em:** Task 06

---

### HIS-03 — Configurar packages e dependências ✅

- [x] MediatR, FluentValidation, EF Core, Npgsql, StackExchange.Redis, Mapster, Testcontainers, NetArchTest, etc.
- [x] Project references corretos entre projetos

---

### HIS-04 — Implementar DbContext e configurações EF Core ✅

- [x] `Infrastructure/Persistence/AuctionDbContext.cs`
- [x] `Infrastructure/Persistence/Configurations/AuctionConfiguration.cs` — xmin como shadow property
- [x] `Infrastructure/Persistence/Configurations/BidConfiguration.cs`
- [x] `Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs`
- [x] `Infrastructure/Persistence/Configurations/IdempotencyKeyConfiguration.cs`
- [x] Connection string em `appsettings.json` e `appsettings.Development.json`
- [x] Migration inicial: `InitialCreate`
- [x] `Infrastructure/Persistence/Repositories/AuctionRepository.cs` (inclui GetExpiredAuctionsAsync)
- [x] `Infrastructure/Persistence/Repositories/BidRepository.cs`

---

### HIS-05 — Implementar Outbox Pattern ✅

- [x] `Infrastructure/Outbox/OutboxMessage.cs`
- [x] `Infrastructure/Persistence/UnitOfWork.cs` — coleta DomainEvents → OutboxMessages na mesma transação
- [x] `BackgroundWorkers/OutboxProcessor.cs` — SKIP LOCKED, batch, retry max 5
- [x] `Infrastructure/Outbox/DomainEventTypeRegistry.cs`

---

## ÉPICO 3 — Application Layer (CQRS + MediatR) ✅ CONCLUÍDO

> **Implementado em:** Task 03 + Task 04 + Task 05

---

### HIS-06 — Implementar PlaceBid ✅

- [x] `Application/Commands/Bids/PlaceBidCommand.cs`
- [x] `Application/Commands/Bids/PlaceBidCommandValidator.cs`
- [x] `Application/Commands/Bids/Handlers/PlaceBidCommandHandler.cs` — com retry de DbUpdateConcurrencyException
- [x] `Application/DTOs/BidDto.cs`
- [x] `Application/Interfaces/IAuctionRepository.cs`
- [x] `Application/Interfaces/IUnitOfWork.cs`

---

### HIS-07 — Implementar CreateAuction ✅

- [x] `Application/Commands/Auctions/CreateAuctionCommand.cs`
- [x] `Application/Commands/Auctions/CreateAuctionCommandValidator.cs`
- [x] `Application/Commands/Auctions/Handlers/CreateAuctionCommandHandler.cs`
- [x] `Application/DTOs/AuctionDto.cs` — Mapster com AuctionMappingConfig

---

### HIS-08 — Implementar Queries ✅

- [x] `Application/Queries/Auctions/GetAuctionDetailsQuery.cs` + Handler
- [x] `Application/Queries/Auctions/GetActiveAuctionsQuery.cs` + Handler (com Redis cache)
- [x] `Application/Queries/Auctions/GetAuctionBidsQuery.cs` + Handler
- [ ] `Application/Queries/Bids/GetUserBidHistoryQuery.cs` + Handler — ❌ não implementado

---

### HIS-09 — Implementar Pipeline Behaviors ✅

- [x] `Application/Behaviors/ValidationBehavior.cs`
- [x] `Application/Behaviors/LoggingBehavior.cs`
- [x] `Application/Behaviors/IdempotencyBehavior.cs`
- [x] `Application/Behaviors/PerformanceBehavior.cs`

---

## ÉPICO 4 — API: Controllers, Middleware, Filters ✅ CONCLUÍDO

> **Implementado em:** Task 10 + Task 11

---

### HIS-10 — Implementar AuctionsController ✅

- [x] `API/Controllers/AuctionsController.cs` — todos os endpoints (GET/, POST/, GET/{id}, POST/{id}/close, POST/{id}/bids, GET/{id}/bids)
- [x] Swagger/OpenAPI com versionamento v1
- [x] `[RateLimit]` em `POST /{id}/bids`

---

### HIS-11 — Implementar BidsController e UsersController ✅

- [x] `API/Controllers/BidsController.cs` — `DELETE /bids/{id}`
- [x] `API/Controllers/UsersController.cs` — `GET /users/{id}/bids` (typo corrigido)

---

### HIS-12 — Implementar Middleware e Filters ✅

- [x] `API/Middleware/CorrelationIdMiddleware.cs`
- [x] `API/Middleware/RequestLoggingMiddleware.cs`
- [x] `API/Middleware/SecurityHeadersMiddleware.cs`
- [x] `API/Filters/GlobalExceptionFilter.cs` — RFC 7807 ProblemDetails
- [x] `API/Filters/IdempotencyFilter.cs`
- [x] `API/Filters/RateLimitAttribute.cs`

---

### HIS-13 — Implementar Rate Limiting Distribuído ✅

- [x] `Infrastructure/RateLimiting/RedisRateLimiter.cs` — Lua script sliding window
- [x] `API/Filters/RateLimitAttribute.cs` — IAsyncActionFilter

---

## ÉPICO 5 — Real-Time: SignalR Hubs ✅ CONCLUÍDO

> **Implementado em:** Task 09

---

### HIS-14 — Implementar AuctionHub ✅

- [x] `API/Hubs/AuctionHub.cs` — JoinAuction, LeaveAuction, RequestStateSync
- [x] `Infrastructure/SignalR/ConnectionTracker.cs` — Redis sets + 24h TTL
- [x] `API/Services/SignalRNotificationService.cs` — broadcasts (na API layer)
- [x] Redis backplane configurado no `Program.cs`
- [x] Hub routes mapeadas (`/hubs/auction`, `/hubs/notifications`)
- [ ] `GracefulShutdownService` — ❌ não implementado

---

### HIS-15 — Integrar broadcast de eventos com SignalR ✅

- [x] `Application/Interfaces/IRealtimeNotificationService.cs`
- [x] `API/Hubs/NotificationHub.cs`
- [x] Handlers de command chamam IRealtimeNotificationService após persistência

---

## ÉPICO 6 — Autenticação e Autorização ❌ NÃO IMPLEMENTADO

> **Status:** Bloqueante para produção. Deferred — sem prazo definido.

---

### HIS-16 — Configurar autenticação JWT ❌

- [ ] `Infrastructure/Security/TokenBlacklistService.cs`
- [ ] Configurar JWT no `Program.cs` com Cognito authority
- [ ] `appsettings.json` com seção `AWS:Cognito`

---

### HIS-17 — Implementar Authorization Policies ❌

- [ ] `Infrastructure/Authorization/CanBidAuthorizationHandler.cs`
- [ ] `Infrastructure/Authorization/CanCreateAuctionAuthorizationHandler.cs`
- [ ] `Infrastructure/Authorization/HighValueBidAuthorizationHandler.cs`

---

## ÉPICO 7 — Background Workers ✅ CONCLUÍDO

> **Implementado em:** Task 13

---

### HIS-18 — Implementar AuctionCloseWorker ✅

- [x] `Application/Commands/Auctions/CloseAuctionCommand.cs` + Handler
- [x] `BackgroundWorkers/Workers/AuctionCloseWorker.cs` — polling a cada 10s
- [x] Registrado como hosted service no `BackgroundWorkers/Program.cs`

---

### HIS-19 — Implementar CleanupExpiredDataWorker ✅

- [x] `BackgroundWorkers/Workers/CleanupExpiredDataWorker.cs` — purge hourly
- [x] `Infrastructure/IDataCleanupService` — purge expired idempotency keys + old outbox
- [ ] `BackgroundWorkers/Jobs/SnapshotCreationJob.cs` — ❌ Fase 2
- [ ] `BackgroundWorkers/Jobs/ReportGenerationJob.cs` — ❌ Fase 2

---

## ÉPICO 8 — Saga: AuctionClosing ❌ FASE 2

---

### HIS-20 — Implementar AuctionClosingSaga ❌

- [ ] `Domain/Sagas/AuctionClosingSagaState.cs`
- [ ] `Application/Sagas/ISagaStateRepository.cs`
- [ ] `Application/Sagas/AuctionClosingSagaHandler.cs`
- [ ] `Infrastructure/Persistence/Repositories/SagaStateRepository.cs`

---

## ÉPICO 9 — Event Sourcing (DynamoDB) ❌ FASE 2

---

### HIS-21 — Implementar IEventStore com DynamoDB ❌

- [ ] `Application/Interfaces/IEventStore.cs`
- [ ] `Infrastructure/EventStore/DynamoDbEventStore.cs`
- [ ] `Infrastructure/EventStore/EventSourcedAuctionRepository.cs`
- [ ] `Domain/Aggregates/Auction/AuctionSnapshot.cs`

---

## ÉPICO 10 — Health Checks e Observabilidade

---

### HIS-22 — Implementar Health Checks customizados ✅

- [x] `Infrastructure/Monitoring/HealthChecks/DatabaseHealthCheck.cs`
- [x] `Infrastructure/Monitoring/HealthChecks/RedisHealthCheck.cs`
- [x] `Infrastructure/Monitoring/HealthChecks/OutboxHealthCheck.cs`
- [x] Endpoints `/health/live`, `/health/ready`, `/health/db`

---

### HIS-23 — Implementar métricas Prometheus ❌ FASE 2

- [ ] `Infrastructure/Monitoring/PrometheusMetrics.cs`
- [ ] Endpoint `/metrics`
- [ ] Instrumentar PlaceBidCommandHandler

---

## ÉPICO 11 — Tests

---

### HIS-24 — Testes Unitários do Domínio ✅

- [x] `UnitTests/Domain/AuctionTests.cs`
- [x] `UnitTests/Domain/BidTests.cs`
- [x] `UnitTests/Domain/ValueObjectTests.cs`
- [x] Unit tests Application (handlers, queries, behaviors)
- [x] Unit tests Shared (AggregateRoot, Entity, Result)
- [x] Unit tests Infrastructure (OutboxProcessor)

---

### HIS-25 — Testes de Arquitetura ⬜ PENDENTE

- [ ] `ArchitectureTests/DependencyTests.cs` — Domain não referencia Infrastructure/Application
- [ ] `ArchitectureTests/NamingConventionTests.cs` — naming conventions dos handlers

---

### HIS-26 — Testes de Integração com Testcontainers ✅

- [x] `IntegrationTests/Fixtures/PostgreSqlFixture.cs`
- [x] `IntegrationTests/Fixtures/RedisFixture.cs`
- [x] `IntegrationTests/Infrastructure/AuctionRepositoryTests.cs`
- [x] `IntegrationTests/Infrastructure/BidRepositoryTests.cs`
- [x] `IntegrationTests/Infrastructure/RedisCacheServiceTests.cs`
- [x] `IntegrationTests/Infrastructure/RedisRateLimiterTests.cs`
- [x] `IntegrationTests/PlaceBidConcurrencyTests.cs`
- [x] `IntegrationTests/PlaceBidIdempotencyTests.cs`
- [ ] `IntegrationTests/API/AuctionsControllerTests.cs` — ❌ WebApplicationFactory end-to-end pendente

---

## ÉPICO 12 — Docker e Deploy

---

### HIS-27 — Dockerfiles e docker-compose completo ❌

- [ ] `docker/Dockerfile.api`
- [ ] `docker/Dockerfile.worker`
- [ ] `docker/docker-compose.yml` — API + Worker services

---

### HIS-28 — Terraform e IaC ❌ FASE 2

- [ ] `iac/terraform/modules/vpc/`
- [ ] `iac/terraform/modules/ecs/`
- [ ] `iac/terraform/modules/rds/`
- [ ] `iac/terraform/modules/elasticache/`
- [ ] `iac/terraform/modules/dynamodb/`
- [ ] `iac/terraform/modules/cognito/`

---

## ÉPICO 13 — CI/CD 🔄 EM PROGRESSO

---

### HIS-29 — GitHub Actions CI 🔄

- [x] Checkout, .NET 10 setup, NuGet cache
- [x] Release build
- [x] Unit tests com coverage upload
- [ ] Architecture tests (NetArchTest)
- [ ] Integration tests com Docker service containers
- [ ] Lint / format check

---

## Resumo de Status por Épico

| Épico                            | Status                             |
| -------------------------------- | ---------------------------------- |
| 1 — Shared + Domain              | ✅ Concluído                       |
| 2 — EF Core + PostgreSQL         | ✅ Concluído                       |
| 3 — Application CQRS             | ✅ Concluído                       |
| 4 — API Controllers + Middleware | ✅ Concluído                       |
| 5 — SignalR Hubs                 | ✅ Concluído                       |
| 6 — Autenticação/Autorização     | ❌ Pendente (bloqueante para prod) |
| 7 — Background Workers           | ✅ Concluído                       |
| 8 — Sagas                        | ❌ Fase 2                          |
| 9 — Event Sourcing DynamoDB      | ❌ Fase 2                          |
| 10 — Health Checks               | ✅ Concluído                       |
| 10 — Prometheus                  | ❌ Fase 2                          |
| 11 — Unit + Integration Tests    | ✅ Concluído                       |
| 11 — Architecture Tests          | ⬜ Pendente                        |
| 11 — Load Tests                  | ⬜ Pendente                        |
| 12 — Dockerfiles                 | ❌ Pendente                        |
| 12 — Terraform                   | ❌ Fase 2                          |
| 13 — CI Pipeline                 | 🔄 Em progresso                    |
