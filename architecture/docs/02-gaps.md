# Lacunas vs Arquitetura — AuctionKings

> Comparação entre a especificação em `/architecture/ARCHITECTURE.md` e o estado atual do repositório.
> Atualizado em: 2026-04-10

---

## Legenda

| Símbolo | Significado                                    |
| ------- | ---------------------------------------------- |
| ✅      | Implementado e funcional                       |
| 🟡      | Parcialmente implementado                      |
| ❌      | Não implementado — gap                         |
| ⚠️      | Desvio da arquitetura ou problema identificado |

---

## 1. Domínio (Domain Layer)

### Aggregates

| Componente                          | Arquivo                                | Status                                                  |
| ----------------------------------- | -------------------------------------- | ------------------------------------------------------- |
| `Auction` aggregate root            | `Domain/Aggregates/Auction/Auction.cs` | ✅                                                      |
| `Auction.PlaceBid()` method         | `Auction.cs`                           | ✅ com retry de concorrência                            |
| `Auction.Version` (xmin token)      | `AuctionConfiguration.cs`              | ✅ shadow property                                      |
| `Auction.AutoExtend` (anti-sniping) | `Auction.cs`                           | ✅ extensão automática                                  |
| `Bid` entity                        | `Domain/Aggregates/Auction/Bid.cs`     | ✅                                                      |
| `AuctionSnapshot`                   | —                                      | ❌ Event Sourcing não implementado (Fase 2)             |
| `User` aggregate                    | —                                      | ❌ Auth via Cognito — aggregate não necessário para MVP |

### Value Objects

| Componente        | Arquivo                                  | Status |
| ----------------- | ---------------------------------------- | ------ |
| `Money`           | `Domain/ValueObjects/Money.cs`           | ✅     |
| `AuctionDuration` | `Domain/ValueObjects/AuctionDuration.cs` | ✅     |
| `BidIncrement`    | `Domain/ValueObjects/BidIncrement.cs`    | ✅     |

### Domain Events

| Evento                  | Arquivo                                  | Status |
| ----------------------- | ---------------------------------------- | ------ |
| `AuctionCreatedEvent`   | `Domain/Events/AuctionCreatedEvent.cs`   | ✅     |
| `BidPlacedEvent`        | `Domain/Events/BidPlacedEvent.cs`        | ✅     |
| `AuctionExtendedEvent`  | `Domain/Events/AuctionExtendedEvent.cs`  | ✅     |
| `AuctionClosedEvent`    | `Domain/Events/AuctionClosedEvent.cs`    | ✅     |
| `WinnerDeterminedEvent` | `Domain/Events/WinnerDeterminedEvent.cs` | ✅     |

### Erros e Enums

| Componente           | Arquivo                         | Status                                |
| -------------------- | ------------------------------- | ------------------------------------- |
| `DomainErrors`       | `Domain/Errors/DomainErrors.cs` | ✅                                    |
| `AuctionStatus` enum | `Domain/Enums/AuctionStatus.cs` | ✅ Pending, Active, Closed, Cancelled |
| `BidStatus` enum     | `Domain/Enums/BidStatus.cs`     | ✅                                    |

### Shared Base Classes

| Componente                | Arquivo                                            | Status                                             |
| ------------------------- | -------------------------------------------------- | -------------------------------------------------- |
| `Entity` base             | `Shared/Common/Entity.cs`                          | ✅ Id, igualdade, GetHashCode                      |
| `AggregateRoot` base      | `Shared/Common/AggregateRoot.cs`                   | ✅ DomainEvents, AddDomainEvent, ClearDomainEvents |
| `IDomainEvent` interface  | `Shared/Common/IDomainEvent.cs`                    | ✅ com OccurredAt                                  |
| `DomainEventTypeRegistry` | `Infrastructure/Outbox/DomainEventTypeRegistry.cs` | ✅                                                 |
| `Result<T>` pattern       | `Shared/Common/Result.cs`                          | ✅                                                 |
| `PagedResult<T>`          | `Shared/Common/PagedResult.cs`                     | ✅                                                 |
| `IDateTimeProvider`       | `Shared/Interfaces/IDateTimeProvider.cs`           | ✅                                                 |
| `IIdempotentCommand<T>`   | `Shared/Interfaces/IIdempotentCommand.cs`          | ✅                                                 |

---

## 2. Application Layer (CQRS)

### Commands

| Componente                                   | Status                                |
| -------------------------------------------- | ------------------------------------- |
| `PlaceBidCommand` + Handler + Validator      | ✅ idempotente, retry de concorrência |
| `CreateAuctionCommand` + Handler + Validator | ✅                                    |
| `CloseAuctionCommand` + Handler + Validator  | ✅                                    |
| `CancelBidCommand`                           | ❌ não implementado                   |

### Queries

| Componente                         | Status              |
| ---------------------------------- | ------------------- |
| `GetAuctionDetailsQuery` + Handler | ✅                  |
| `GetActiveAuctionsQuery` + Handler | ✅ com Redis cache  |
| `GetAuctionBidsQuery` + Handler    | ✅                  |
| `GetUserBidHistoryQuery` + Handler | ❌ não implementado |

### Pipeline Behaviors (MediatR)

| Componente            | Status                                |
| --------------------- | ------------------------------------- |
| `ValidationBehavior`  | ✅ FluentValidation → erro 400 tipado |
| `LoggingBehavior`     | ✅ início/fim com correlationId       |
| `IdempotencyBehavior` | ✅ verifica cache distribuído         |
| `PerformanceBehavior` | ✅ warning se > 500ms                 |

### DTOs e Mapping

| Componente             | Status                                                            |
| ---------------------- | ----------------------------------------------------------------- |
| `AuctionDto`, `BidDto` | ✅ Mapster com `IRegister`                                        |
| `AuctionMappingConfig` | ✅ `ConstructUsing` para Money flattening, top-10 bids projection |
| `AuctionStateDto`      | ❌ não implementado                                               |
| `UserDto`              | ❌ não necessário sem User aggregate                              |

### Validators (FluentValidation)

| Componente                      | Status |
| ------------------------------- | ------ |
| `PlaceBidCommandValidator`      | ✅     |
| `CreateAuctionCommandValidator` | ✅     |

### Sagas

| Componente                  | Status    |
| --------------------------- | --------- |
| `AuctionClosingSagaState`   | ❌ Fase 2 |
| `AuctionClosingSagaHandler` | ❌ Fase 2 |
| `ISagaStateRepository`      | ❌ Fase 2 |

---

## 3. Infrastructure Layer

### Persistence (EF Core + PostgreSQL)

| Componente                         | Status                                                     |
| ---------------------------------- | ---------------------------------------------------------- |
| `AuctionDbContext`                 | ✅ DbSets: Auctions, Bids, OutboxMessages, IdempotencyKeys |
| `UnitOfWork` (com Outbox)          | ✅ coleta DomainEvents → OutboxMessages na mesma transação |
| `OutboxMessage` entity             | ✅                                                         |
| `AuctionConfiguration` (xmin)      | ✅ shadow property `IsConcurrencyToken()`                  |
| `BidConfiguration`                 | ✅                                                         |
| `OutboxMessageConfiguration`       | ✅                                                         |
| `IdempotencyKeyConfiguration`      | ✅                                                         |
| `SagaStateConfiguration`           | ❌ Fase 2                                                  |
| `AuctionRepository`                | ✅ incluindo `GetExpiredAuctionsAsync`                     |
| `BidRepository`                    | ✅                                                         |
| `SagaStateRepository`              | ❌ Fase 2                                                  |
| EF Core Migrations (InitialCreate) | ✅                                                         |

### Event Store (DynamoDB)

| Componente                      | Status    |
| ------------------------------- | --------- |
| `IEventStore` interface         | ❌ Fase 2 |
| `DynamoDbEventStore`            | ❌ Fase 2 |
| `EventSourcedAuctionRepository` | ❌ Fase 2 |

### Caching (Redis)

| Componente                 | Status               |
| -------------------------- | -------------------- |
| `RedisCacheService`        | ✅ IDistributedCache |
| `CacheKeys` constants      | ✅                   |
| `CacheInvalidationService` | ✅                   |

### Messaging (SQS/SNS)

| Componente                  | Status                                              |
| --------------------------- | --------------------------------------------------- |
| `SqsMessagePublisher`       | ❌ usando MediatR in-memory; SQS migration pendente |
| `SnsNotificationService`    | ❌                                                  |
| `IEventPublisher` interface | ❌ Outbox usa MediatR diretamente                   |

### Rate Limiting

| Componente                          | Status            |
| ----------------------------------- | ----------------- |
| `IDistributedRateLimiter` interface | ✅                |
| `RedisRateLimiter` (Lua script)     | ✅ sliding window |

### SignalR Services

| Componente                         | Status                                      |
| ---------------------------------- | ------------------------------------------- |
| `SignalRNotificationService`       | ✅ na API layer (evita circular dependency) |
| `ConnectionTracker` (Redis-backed) | ✅ Redis sets + 24h TTL                     |
| `GracefulShutdownService`          | ❌ não implementado                         |

### Security / Authorization

| Componente                         | Status                   |
| ---------------------------------- | ------------------------ |
| `ITokenBlacklistService`           | ❌ Auth não implementado |
| `RedisTokenBlacklistService`       | ❌                       |
| `CognitoService`                   | ❌                       |
| `CanBidAuthorizationHandler`       | ❌                       |
| `HighValueBidAuthorizationHandler` | ❌                       |

### Monitoring

| Componente            | Status                              |
| --------------------- | ----------------------------------- |
| `DatabaseHealthCheck` | ✅ IDatabasePingService abstraction |
| `RedisHealthCheck`    | ✅ IConnectionMultiplexer ping      |
| `OutboxHealthCheck`   | ✅ detecção de mensagens travadas   |
| `PrometheusMetrics`   | ❌ Fase 2                           |
| `XRayTracing`         | ❌ Fase 2                           |

---

## 4. API Layer

### Controllers

| Endpoint               | Verbo + Rota                       | Status          |
| ---------------------- | ---------------------------------- | --------------- |
| Listar leilões ativos  | `GET /api/v1/auctions`             | ✅              |
| Criar leilão           | `POST /api/v1/auctions`            | ✅              |
| Detalhes do leilão     | `GET /api/v1/auctions/{id}`        | ✅              |
| Fechar leilão          | `POST /api/v1/auctions/{id}/close` | ✅              |
| Submeter lance         | `POST /api/v1/auctions/{id}/bids`  | ✅ rate limited |
| Histórico de lances    | `GET /api/v1/auctions/{id}/bids`   | ✅              |
| Histórico do usuário   | `GET /api/v1/users/{id}/bids`      | ✅              |
| Cancelar lance (admin) | `DELETE /api/v1/bids/{id}`         | ✅              |

### Middleware

| Componente                  | Status                           |
| --------------------------- | -------------------------------- |
| `CorrelationIdMiddleware`   | ✅ gera/propaga X-Correlation-Id |
| `RequestLoggingMiddleware`  | ✅ método/path/status/duração    |
| `SecurityHeadersMiddleware` | ✅ OWASP headers                 |

### Filters

| Componente              | Status                     |
| ----------------------- | -------------------------- |
| `GlobalExceptionFilter` | ✅ RFC 7807 ProblemDetails |
| `IdempotencyFilter`     | ✅ lê X-Request-Id         |
| `RateLimitAttribute`    | ✅ IAsyncActionFilter      |

### Hubs (SignalR)

| Hub               | Métodos                                                                  | Status             |
| ----------------- | ------------------------------------------------------------------------ | ------------------ |
| `AuctionHub`      | OnConnected, OnDisconnected, JoinAuction, LeaveAuction, RequestStateSync | ✅ Redis backplane |
| `NotificationHub` | OnConnected, OnDisconnected                                              | ✅ por userId      |

### Program.cs

| Configuração                       | Status                                    |
| ---------------------------------- | ----------------------------------------- |
| `AddMediatR()`                     | ✅                                        |
| `AddDbContext<AuctionDbContext>()` | ✅                                        |
| `AddStackExchangeRedisCache()`     | ✅                                        |
| `AddSignalR()` com Redis backplane | ✅                                        |
| Middlewares customizados           | ✅                                        |
| Hub route mapping                  | ✅ `/hubs/auction`, `/hubs/notifications` |
| `AddAuthentication()` JWT/Cognito  | ❌ não implementado                       |
| `AddAuthorization()` com policies  | ❌ não implementado                       |
| `AddMessagePackProtocol()`         | ❌ não implementado                       |

---

## 5. Background Workers

| Worker                                        | Status                                    |
| --------------------------------------------- | ----------------------------------------- |
| `OutboxProcessor` (SKIP LOCKED, batch, retry) | ✅                                        |
| `AuctionCloseWorker` (polling a cada 10s)     | ✅                                        |
| `CleanupExpiredDataWorker` (hourly purge)     | ✅ purge idempotency keys + old outbox    |
| `BidProcessorWorker`                          | ❌ não necessário (processado no handler) |
| `NotificationWorker`                          | ❌ não necessário (SignalR direto)        |
| `ProjectionWorker`                            | ❌ Fase 2                                 |
| `DlqProcessorWorker`                          | ❌ SQS não implementado                   |
| `SnapshotCreationJob`                         | ❌ Fase 2                                 |
| `ReportGenerationJob`                         | ❌ Fase 2                                 |

---

## 6. Tests

| Categoria                                                | Status                  |
| -------------------------------------------------------- | ----------------------- |
| Unit — Domain (Auction, Bid, ValueObjects)               | ✅                      |
| Unit — Application (handlers, queries, behaviors)        | ✅                      |
| Unit — Shared (AggregateRoot, Entity, Result)            | ✅                      |
| Unit — Infrastructure (OutboxProcessor)                  | ✅                      |
| Integration — Repositories (PostgreSQL real)             | ✅ Testcontainers       |
| Integration — Redis (cache, rate limiter)                | ✅ Testcontainers       |
| Integration — PlaceBid concorrência (n threads)          | ✅                      |
| Integration — PlaceBid idempotência (mesmo X-Request-Id) | ✅                      |
| Architecture — DependencyTests                           | ❌ NetArchTest pendente |
| Architecture — NamingConventionTests                     | ❌ NetArchTest pendente |
| Load — BidStorm (NBomber)                                | ❌ pendente             |

---

## 7. Infraestrutura / Deploy

| Componente                                                          | Status                |
| ------------------------------------------------------------------- | --------------------- |
| `docker-compose.infra.yml` (PostgreSQL + Redis + LocalStack + Moto) | ✅                    |
| `docker-compose.yml` (API + Worker services)                        | ❌ vazio              |
| `Dockerfile.api`                                                    | ❌                    |
| `Dockerfile.worker`                                                 | ❌                    |
| LocalStack init scripts (DynamoDB, SQS, SNS, S3, Secrets Manager)   | ✅                    |
| Moto init script (Cognito, AppConfig, EventBridge)                  | ✅                    |
| `appsettings.LocalStack.json`                                       | ✅ endpoint overrides |
| GitHub Actions CI (build + unit tests + coverage)                   | 🔄 Parcial            |
| GitHub Actions CD (staging/prod)                                    | ❌                    |
| Terraform modules                                                   | ❌ Fase 2             |

---

## 8. Problemas / Desvios Identificados

| #   | Tipo        | Problema                                                           | Impacto                     |
| --- | ----------- | ------------------------------------------------------------------ | --------------------------- |
| 1   | ⚠️ Typo     | `AuctionKings.Applciation.csproj` — persiste                       | Baixo (build funciona)      |
| 2   | ⚠️ Ausência | JWT/Cognito auth não implementado — todos os endpoints públicos    | Alto — bloqueante para prod |
| 3   | ⚠️ Ausência | `docker-compose.yml` vazio — API + Worker não containerizados      | Médio                       |
| 4   | ⚠️ Desvio   | `SignalRNotificationService` em API layer (não em Infrastructure)  | Aceito — evita circular dep |
| 5   | ⚠️ Ausência | Outbox usa MediatR in-memory — não há publicação real para SQS/SNS | Médio — suficiente para MVP |

---

## Resumo das Lacunas por Criticidade

| Prioridade         | Itens Pendentes                                                | Bloqueante para MVP?     |
| ------------------ | -------------------------------------------------------------- | ------------------------ |
| **Crítico**        | JWT/Cognito auth                                               | ✅ Sim (para prod)       |
| **Alto**           | Architecture tests, docker-compose.yml, CI completo            | Não (funcional sem eles) |
| **Médio**          | GetUserBidHistoryQuery, GracefulShutdown, SQS/SNS, MessagePack | Não                      |
| **Baixo (Fase 2)** | Sagas, DynamoDB Event Store, Prometheus, Terraform, Load Tests | Não                      |
