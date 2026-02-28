# Lacunas vs Arquitetura — AuctionKings

> Comparação entre a especificação em `/architecture/ARCHITECTURE.md` e o estado atual do repositório.
> Gerado em: 2026-02-28

---

## Legenda

| Símbolo | Significado |
|---------|-------------|
| ✅ | Implementado e funcional |
| 🟡 | Parcialmente implementado (scaffold/stub) |
| ❌ | Não implementado — gap crítico |
| ⚠️ | Desvio da arquitetura ou problema identificado |

---

## 1. Domínio (Domain Layer)

### Aggregates

| Componente | Arquivo Esperado | Status | Detalhe |
|-----------|-----------------|--------|---------|
| `Auction` aggregate root | `Domain/Aggregates/Auction/Auction.cs` | ❌ | Nenhum arquivo criado |
| `Auction.PlaceBid()` method | `Auction.cs` | ❌ | Lógica de negócio inexistente |
| `Auction.Version` (xmin token) | `Auction.cs` | ❌ | Concorrência otimista não implementada |
| `Auction.AutoExtend` (anti-sniping) | `Auction.cs` | ❌ | Extensão automática não implementada |
| `Bid` entity | `Domain/Aggregates/Auction/Bid.cs` | ❌ | Entidade inexistente |
| `AuctionSnapshot` | `Domain/Aggregates/Auction/AuctionSnapshot.cs` | ❌ | Snapshots de Event Sourcing não implementados |
| `User` aggregate | `Domain/Aggregates/User/User.cs` | ❌ | Inexistente |
| `UserProfile` | `Domain/Aggregates/User/UserProfile.cs` | ❌ | Inexistente |

### Value Objects

| Componente | Arquivo Esperado | Status |
|-----------|-----------------|--------|
| `Money` | `Domain/ValueObjects/Money.cs` | ❌ |
| `AuctionDuration` | `Domain/ValueObjects/AuctionDuration.cs` | ❌ |
| `BidIncrement` | `Domain/ValueObjects/BidIncrement.cs` | ❌ |

### Domain Events

| Evento | Arquivo Esperado | Status |
|--------|-----------------|--------|
| `AuctionCreatedEvent` | `Domain/Events/AuctionCreatedEvent.cs` | ❌ |
| `BidPlacedEvent` | `Domain/Events/BidPlacedEvent.cs` | ❌ |
| `AuctionExtendedEvent` | `Domain/Events/AuctionExtendedEvent.cs` | ❌ |
| `AuctionClosedEvent` | `Domain/Events/AuctionClosedEvent.cs` | ❌ |
| `WinnerDeterminedEvent` | `Domain/Events/WinnerDeterminedEvent.cs` | ❌ |

### Erros e Enums

| Componente | Arquivo Esperado | Status |
|-----------|-----------------|--------|
| `DomainErrors` | `Domain/Errors/DomainErrors.cs` | ❌ |
| `AuctionStatus` enum | `Domain/Enums/AuctionStatus.cs` | ❌ |
| `BidStatus` enum | `Domain/Enums/BidStatus.cs` | ❌ |

### Specifications

| Componente | Status |
|-----------|--------|
| `ActiveAuctionSpecification` | ❌ |
| `BiddableAuctionSpecification` | ❌ |

### Shared Base Classes

| Componente | Arquivo | Status | Detalhe |
|-----------|---------|--------|---------|
| `Entity` base | `Shared/Common/Entity.cs` | 🟡 | Arquivo existe, classe **vazia** |
| `AggregateRoot` base | `Shared/Common/AggregateRoot.cs` | 🟡 | Arquivo existe, classe **vazia** — sem `DomainEvents`, `AddDomainEvent`, `ClearDomainEvents` |
| `IDomainEvent` interface | — | ❌ | Não existe |
| `DomainEventTypeRegistry` | — | ❌ | Não existe |
| `Result<T>` pattern | — | ❌ | Não existe |
| `IDateTimeProvider` | — | ❌ | Não existe |
| `IIdempotentCommand<T>` | — | ❌ | Não existe |

---

## 2. Application Layer (CQRS)

### Commands

| Componente | Arquivo Esperado | Status |
|-----------|-----------------|--------|
| `PlaceBidCommand` | `Application/Commands/Bids/PlaceBidCommand.cs` | ❌ |
| `PlaceBidCommandHandler` | `Application/Commands/Bids/Handlers/PlaceBidCommandHandler.cs` | ❌ |
| `CreateAuctionCommand` | `Application/Commands/Auctions/CreateAuctionCommand.cs` | ❌ |
| `CreateAuctionCommandHandler` | `Application/Commands/Auctions/Handlers/CreateAuctionCommandHandler.cs` | ❌ |
| `CloseAuctionCommand` | `Application/Commands/Auctions/CloseAuctionCommand.cs` | ❌ |
| `CloseAuctionCommandHandler` | `Application/Commands/Auctions/Handlers/CloseAuctionCommandHandler.cs` | ❌ |
| `CancelBidCommand` | `Application/Commands/Bids/CancelBidCommand.cs` | ❌ |

### Queries

| Componente | Arquivo Esperado | Status |
|-----------|-----------------|--------|
| `GetAuctionDetailsQuery` | `Application/Queries/Auctions/GetAuctionDetailsQuery.cs` | ❌ |
| `GetAuctionBidsQuery` | `Application/Queries/Auctions/GetAuctionBidsQuery.cs` | ❌ |
| `GetActiveAuctionsQuery` | `Application/Queries/Auctions/GetActiveAuctionsQuery.cs` | ❌ |
| `GetUserBidHistoryQuery` | `Application/Queries/Bids/GetUserBidHistoryQuery.cs` | ❌ |

### Pipeline Behaviors (MediatR)

| Componente | Arquivo Esperado | Status |
|-----------|-----------------|--------|
| `ValidationBehavior` | `Application/Behaviors/ValidationBehavior.cs` | ❌ |
| `LoggingBehavior` | `Application/Behaviors/LoggingBehavior.cs` | ❌ |
| `IdempotencyBehavior` | `Application/Behaviors/IdempotencyBehavior.cs` | ❌ |
| `PerformanceBehavior` | `Application/Behaviors/PerformanceBehavior.cs` | ❌ |

### DTOs

| Componente | Status |
|-----------|--------|
| `AuctionDto` | ❌ |
| `AuctionStateDto` | ❌ |
| `BidDto` | ❌ |
| `UserDto` | ❌ |

### Validators (FluentValidation)

| Componente | Status |
|-----------|--------|
| `PlaceBidCommandValidator` | ❌ |
| `CreateAuctionCommandValidator` | ❌ |

### Sagas

| Componente | Status |
|-----------|--------|
| `AuctionClosingSagaState` | ❌ |
| `AuctionClosingSagaHandler` | ❌ |
| `ISagaStateRepository` | ❌ |

---

## 3. Infrastructure Layer

### Persistence (EF Core + PostgreSQL)

| Componente | Arquivo Esperado | Status |
|-----------|-----------------|--------|
| `AuctionDbContext` | `Infrastructure/Persistence/AuctionDbContext.cs` | ❌ |
| `UnitOfWork` (com Outbox) | `Infrastructure/Persistence/UnitOfWork.cs` | ❌ |
| `OutboxMessage` entity | `Infrastructure/Persistence/OutboxMessage.cs` | ❌ |
| `AuctionConfiguration` (xmin) | `Infrastructure/Persistence/Configurations/AuctionConfiguration.cs` | ❌ |
| `BidConfiguration` | `Infrastructure/Persistence/Configurations/BidConfiguration.cs` | ❌ |
| `OutboxMessageConfiguration` | `Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs` | ❌ |
| `SagaStateConfiguration` | `Infrastructure/Persistence/Configurations/SagaStateConfiguration.cs` | ❌ |
| `AuctionRepository` | `Infrastructure/Persistence/Repositories/AuctionRepository.cs` | ❌ |
| `BidRepository` | `Infrastructure/Persistence/Repositories/BidRepository.cs` | ❌ |
| `SagaStateRepository` | `Infrastructure/Persistence/Repositories/SagaStateRepository.cs` | ❌ |
| EF Core Migrations | `Infrastructure/Persistence/Migrations/` | ❌ |

### Event Store (DynamoDB)

| Componente | Status |
|-----------|--------|
| `IEventStore` interface | ❌ |
| `DynamoDbEventStore` | ❌ |
| `EventSourcedAuctionRepository` | ❌ |
| `ConcurrencyException` | ❌ |

### Caching (Redis)

| Componente | Status |
|-----------|--------|
| `RedisCacheService` | ❌ |
| `CacheKeys` constants | ❌ |
| `CacheInvalidationService` | ❌ |

### Messaging (SQS/SNS)

| Componente | Status |
|-----------|--------|
| `SqsMessagePublisher` | ❌ |
| `SnsNotificationService` | ❌ |
| `IEventPublisher` interface | ❌ |

### Rate Limiting

| Componente | Status |
|-----------|--------|
| `IDistributedRateLimiter` interface | ❌ |
| `RedisRateLimiter` (Lua script) | ❌ |

### SignalR Services

| Componente | Status |
|-----------|--------|
| `SignalRNotificationService` | ❌ |
| `ConnectionTracker` (Redis-backed) | ❌ |
| `GracefulShutdownService` | ❌ |

### Security / Authorization

| Componente | Status |
|-----------|--------|
| `ITokenBlacklistService` | ❌ |
| `RedisTokenBlacklistService` | ❌ |
| `CognitoService` | ❌ |
| `CanBidAuthorizationHandler` | ❌ |
| `CanCreateAuctionAuthorizationHandler` | ❌ |
| `HighValueBidAuthorizationHandler` | ❌ |

### Background Services

| Componente | Status |
|-----------|--------|
| `OutboxProcessor` | ❌ |
| `SecretsRotationService` | ❌ |

### Monitoring

| Componente | Status |
|-----------|--------|
| `PrometheusMetrics` | ❌ |
| `DatabaseHealthCheck` | ❌ |
| `RedisHealthCheck` | ❌ |
| `SignalRHealthCheck` | ❌ |
| `XRayTracing` | ❌ |

---

## 4. API Layer

### Controllers

| Endpoint | Verbo + Rota | Status |
|---------|-------------|--------|
| Listar leilões ativos | `GET /api/v1/auctions` | ❌ |
| Criar leilão | `POST /api/v1/auctions` | ❌ |
| Detalhes do leilão | `GET /api/v1/auctions/{id}` | ❌ |
| Fechar leilão | `POST /api/v1/auctions/{id}/close` | ❌ |
| Submeter lance | `POST /api/v1/auctions/{id}/bids` | ❌ |
| Histórico de lances | `GET /api/v1/auctions/{id}/bids` | ❌ |
| Histórico do usuário | `GET /api/v1/users/{id}/bids` | ❌ |
| Cancelar lance (admin) | `DELETE /api/v1/bids/{id}` | ❌ |

### Versionamento de API

| Componente | Status |
|-----------|--------|
| `[ApiVersion]` nos controllers | ❌ |
| `api/v{version:apiVersion}/` nas rotas | ❌ |
| V2 controllers (futuro) | ❌ |

### Middleware

| Componente | Status |
|-----------|--------|
| `CorrelationIdMiddleware` | ❌ |
| `RequestLoggingMiddleware` | ❌ |
| `SecurityHeadersMiddleware` | ❌ |

### Filters

| Componente | Status |
|-----------|--------|
| `GlobalExceptionFilter` | 🟡 (stub vazio) |
| `IdempotencyFilter` | 🟡 (stub vazio) |
| `RateLimitAttribute` | 🟡 (comentado) |

### Hubs (SignalR)

| Hub | Métodos Esperados | Status |
|----|------------------|--------|
| `AuctionHub` | `OnConnected`, `OnDisconnected`, `JoinAuction`, `LeaveAuction`, `RequestStateSync` | 🟡 (stub sem métodos) |
| `NotificationHub` | `OnConnected`, `OnDisconnected` | 🟡 (stub sem métodos) |

### Program.cs — Configurações Ausentes

| Configuração | Status |
|-------------|--------|
| `AddSignalR()` com Redis backplane | ❌ |
| `AddMessagePackProtocol()` | ❌ |
| `AddAuthentication()` com JWT/Cognito | ❌ |
| `AddAuthorization()` com policies | ❌ |
| `AddMediatR()` | ❌ |
| `AddDbContext<AuctionDbContext>()` | ❌ |
| `AddStackExchangeRedisCache()` | ❌ |
| Middlewares customizados | ❌ |
| Hub route mapping (`/hubs/auction`, `/hubs/notifications`) | ❌ |
| Rate limiting middleware | ❌ |

---

## 5. Background Workers

| Worker | Status |
|--------|--------|
| `OutboxProcessor` (polling outbox a cada 1s) | ❌ |
| `AuctionCloseWorker` (timer distribuído Redis) | ❌ |
| `BidProcessorWorker` | ❌ |
| `NotificationWorker` | ❌ |
| `ProjectionWorker` | ❌ |
| `DlqProcessorWorker` (Dead Letter Queue) | ❌ |
| `CleanupExpiredDataJob` | ❌ |
| `SnapshotCreationJob` | ❌ |
| `ReportGenerationJob` | ❌ |

---

## 6. Tests

| Categoria | Arquivo Esperado | Status |
|-----------|-----------------|--------|
| Unit — Domain `AuctionTests` | `UnitTests/Domain/AuctionTests.cs` | ❌ |
| Unit — Domain `BidTests` | `UnitTests/Domain/BidTests.cs` | ❌ |
| Unit — Application `PlaceBidCommandHandlerTests` | `UnitTests/Application/PlaceBidCommandHandlerTests.cs` | ❌ |
| Unit — `IdempotencyBehaviorTests` | `UnitTests/Application/IdempotencyBehaviorTests.cs` | ❌ |
| Unit — `RedisRateLimiterTests` | `UnitTests/Infrastructure/RedisRateLimiterTests.cs` | ❌ |
| Integration — `AuctionsControllerTests` | `IntegrationTests/API/AuctionsControllerTests.cs` | ❌ |
| Integration — `RepositoryTests` | `IntegrationTests/Infrastructure/RepositoryTests.cs` | ❌ |
| Integration — `OutboxProcessorTests` | `IntegrationTests/Infrastructure/OutboxProcessorTests.cs` | ❌ |
| Architecture — `DependencyTests` | `ArchitectureTests/DependencyTests.cs` | ❌ |
| Architecture — `NamingConventionTests` | `ArchitectureTests/NamingConventionTests.cs` | ❌ |
| Load — `BidStormTest` (k6) | `LoadTests/k6/auction-load-test.js` | ❌ |

---

## 7. Infraestrutura / Deploy

| Componente | Status |
|-----------|--------|
| `docker-compose.infra.yml` (PostgreSQL + Redis) | ✅ |
| `docker-compose.yml` (API + Worker services) | ❌ (arquivo vazio) |
| `Dockerfile.api` | ❌ |
| `Dockerfile.worker` | ❌ |
| Terraform modules (VPC, ECS, RDS, ElastiCache, DynamoDB, Cognito) | ❌ |
| GitHub Actions CI | 🟡 (configurado em commits, não verificado) |
| GitHub Actions CD (staging/prod) | ❌ |

---

## 8. Problemas / Desvios Identificados

| # | Tipo | Problema | Impacto |
|---|------|---------|---------|
| 1 | ⚠️ Typo | `AuctionKings.Applciation.csproj` — falta o 'i' | Baixo (build funciona) |
| 2 | ⚠️ Desvio | Rota atual `api/[controller]` vs arquitetura `api/v{version:apiVersion}/` | Alto — quebra versioning |
| 3 | ⚠️ Desvio | `UsersControllers.cs` — nome no plural (deveria ser `UsersController.cs`) | Baixo |
| 4 | ⚠️ Ausência | Nenhum package EF Core / MediatR / SignalR / Redis em `Directory.Packages.props` | Crítico — impossibilita build funcional |
| 5 | ⚠️ Desvio | `AggregateRoot` sem suporte a DomainEvents — não pode ser usado pelo Domain | Crítico |
| 6 | ⚠️ Ausência | `docker-compose.yml` vazio — não há como subir API + Worker em container | Alto |

---

## Resumo das Lacunas por Criticidade

| Prioridade | Quantidade de Itens Faltando | Bloqueante para MVP? |
|-----------|------------------------------|----------------------|
| **Crítico** — bloqueia o desenvolvimento | ~15 | ✅ Sim |
| **Alto** — funcionalidade core ausente | ~40 | Sim |
| **Médio** — funcionalidade secundária | ~25 | Não |
| **Baixo** — observabilidade, IaC, load tests | ~20 | Não |
| **Total** | **~100 itens** | — |
