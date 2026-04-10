# Inventário do Repositório — AuctionKings

> Atualizado em: 2026-04-10
> Fonte de verdade da arquitetura: `/architecture/ARCHITECTURE.md`

---

## 1. Projetos (.csproj)

| Projeto                        | Caminho                                       | Tipo                 | Status                  |
| ------------------------------ | --------------------------------------------- | -------------------- | ----------------------- |
| AuctionKings.API               | `src/services/AuctionKings.API/`              | ASP.NET Core Web API | ✅ Implementado         |
| AuctionKings.Application       | `src/services/AuctionKings.Application/`      | Class Library        | ✅ Implementado         |
| AuctionKings.Domain            | `src/services/AuctionKings.Domain/`           | Class Library        | ✅ Implementado         |
| AuctionKings.Infrastructure    | `src/services/AuctionKings.Infrastructure/`   | Class Library        | ✅ Implementado         |
| AuctionKings.Shared            | `src/common/AuctionKings.Shared/`             | Class Library        | ✅ Implementado         |
| AuctionKings.BackgroundWorkers | `src/workers/AuctionKings.BackgroundWorkers/` | Console App          | ✅ Implementado         |
| AuctionKings.UnitTests         | `test/AuctionKings.UnitTests/`                | xUnit                | ✅ 118+ testes passando |
| AuctionKings.IntegrationTests  | `test/AuctionKings.IntegrationTests/`         | xUnit                | ✅ Testcontainers       |
| AuctionKings.ArchitectureTests | `test/AuctionKings.ArchitectureTests/`        | xUnit                | ⬜ Pendente             |
| AuctionKings.LoadTests         | `test/AuctionKings.LoadTests/`                | xUnit                | ⬜ Pendente             |

> **Observação:** typo no .csproj da Application (`AuctionKings.Applciation.csproj`) persiste — build funciona normalmente.

---

## 2. Árvore de Diretórios (estado atual)

```
auction-kings/
├── src/
│   ├── common/
│   │   └── AuctionKings.Shared/
│   │       └── Common/
│   │           ├── Entity.cs               — ✅ Id, igualdade por valor, GetHashCode
│   │           ├── AggregateRoot.cs        — ✅ DomainEvents list, AddDomainEvent, ClearDomainEvents
│   │           ├── IDomainEvent.cs         — ✅ interface com OccurredAt
│   │           ├── Result.cs               — ✅ Result<T> + Error pattern
│   │           ├── PagedResult.cs          — ✅ paginação com metadata
│   │           └── IDateTimeProvider.cs    — ✅ interface + SystemDateTimeProvider
│   ├── services/
│   │   ├── AuctionKings.API/
│   │   │   ├── Controllers/
│   │   │   │   ├── AuctionsController.cs   — ✅ GET/, POST/, GET/{id}, POST/{id}/close, GET/{id}/bids
│   │   │   │   ├── BidsController.cs       — ✅ DELETE /bids/{id}
│   │   │   │   └── UsersController.cs      — ✅ GET /users/{id}/bids (typo corrigido)
│   │   │   ├── Filters/
│   │   │   │   ├── GlobalExceptionFilter.cs — ✅ RFC 7807 ProblemDetails
│   │   │   │   ├── IdempotencyFilter.cs    — ✅ lê X-Request-Id header
│   │   │   │   └── RateLimitAttribute.cs   — ✅ IAsyncActionFilter implementado
│   │   │   ├── Hubs/
│   │   │   │   ├── AuctionHub.cs           — ✅ JoinAuction, LeaveAuction, RequestStateSync
│   │   │   │   └── NotificationHub.cs      — ✅ OnConnected, OnDisconnected por usuário
│   │   │   ├── Middleware/
│   │   │   │   ├── CorrelationIdMiddleware.cs    — ✅ gera/propaga X-Correlation-Id
│   │   │   │   ├── RequestLoggingMiddleware.cs   — ✅ método/path/status/duração
│   │   │   │   └── SecurityHeadersMiddleware.cs  — ✅ OWASP headers
│   │   │   ├── Services/
│   │   │   │   └── SignalRNotificationService.cs — ✅ na API layer (evita circular dep)
│   │   │   └── Program.cs                  — ✅ MediatR, EF Core, Redis, SignalR, Health Checks, Middlewares
│   │   ├── AuctionKings.Application/
│   │   │   ├── Behaviors/
│   │   │   │   ├── ValidationBehavior.cs   — ✅
│   │   │   │   ├── LoggingBehavior.cs      — ✅
│   │   │   │   ├── IdempotencyBehavior.cs  — ✅ verifica cache antes de processar
│   │   │   │   └── PerformanceBehavior.cs  — ✅ warning se > 500ms
│   │   │   ├── Commands/
│   │   │   │   ├── Auctions/
│   │   │   │   │   ├── CreateAuctionCommand.cs + Handler + Validator — ✅
│   │   │   │   │   └── CloseAuctionCommand.cs + Handler + Validator  — ✅
│   │   │   │   └── Bids/
│   │   │   │       └── PlaceBidCommand.cs + Handler + Validator      — ✅ com retry de concorrência
│   │   │   ├── Queries/
│   │   │   │   └── Auctions/
│   │   │   │       ├── GetAuctionDetailsQuery.cs + Handler  — ✅
│   │   │   │       ├── GetActiveAuctionsQuery.cs + Handler  — ✅ com Redis cache
│   │   │   │       └── GetAuctionBidsQuery.cs + Handler     — ✅
│   │   │   ├── DTOs/
│   │   │   │   ├── AuctionDto.cs, BidDto.cs — ✅ Mapster com IRegister config
│   │   │   │   └── AuctionMappingConfig.cs  — ✅ ConstructUsing para Money flattening
│   │   │   └── Interfaces/
│   │   │       ├── IAuctionRepository.cs, IBidRepository.cs — ✅
│   │   │       ├── IUnitOfWork.cs                           — ✅
│   │   │       ├── ICacheService.cs                         — ✅
│   │   │       ├── IIdempotencyService.cs                   — ✅
│   │   │       └── IRealtimeNotificationService.cs          — ✅
│   │   ├── AuctionKings.Domain/
│   │   │   ├── Aggregates/Auction/
│   │   │   │   ├── Auction.cs              — ✅ Create, PlaceBid, Close, auto-extend anti-sniping
│   │   │   │   └── Bid.cs                  — ✅ entity
│   │   │   ├── ValueObjects/
│   │   │   │   ├── Money.cs                — ✅ imutável, validação positivo
│   │   │   │   ├── AuctionDuration.cs      — ✅
│   │   │   │   └── BidIncrement.cs         — ✅
│   │   │   ├── Events/
│   │   │   │   ├── AuctionCreatedEvent.cs  — ✅
│   │   │   │   ├── BidPlacedEvent.cs       — ✅
│   │   │   │   ├── AuctionExtendedEvent.cs — ✅
│   │   │   │   ├── AuctionClosedEvent.cs   — ✅
│   │   │   │   └── WinnerDeterminedEvent.cs — ✅
│   │   │   ├── Errors/DomainErrors.cs      — ✅ erros estáticos agrupados
│   │   │   └── Enums/
│   │   │       ├── AuctionStatus.cs        — ✅ Pending, Active, Closed, Cancelled
│   │   │       └── BidStatus.cs            — ✅
│   │   └── AuctionKings.Infrastructure/
│   │       ├── Persistence/
│   │       │   ├── AuctionDbContext.cs      — ✅ DbSets: Auctions, Bids, OutboxMessages, IdempotencyKeys
│   │       │   ├── UnitOfWork.cs            — ✅ coleta DomainEvents → OutboxMessages na mesma transação
│   │       │   ├── Configurations/
│   │       │   │   ├── AuctionConfiguration.cs        — ✅ xmin como concurrency token (shadow property)
│   │       │   │   ├── BidConfiguration.cs            — ✅
│   │       │   │   ├── OutboxMessageConfiguration.cs  — ✅
│   │       │   │   └── IdempotencyKeyConfiguration.cs — ✅
│   │       │   ├── Repositories/
│   │       │   │   ├── AuctionRepository.cs — ✅
│   │       │   │   └── BidRepository.cs     — ✅
│   │       │   └── Migrations/InitialCreate — ✅
│   │       ├── Caching/
│   │       │   ├── RedisCacheService.cs          — ✅ IDistributedCache
│   │       │   ├── CacheInvalidationService.cs   — ✅
│   │       │   └── CacheKeys.cs                  — ✅
│   │       ├── RateLimiting/
│   │       │   └── RedisRateLimiter.cs     — ✅ sliding window com Lua script
│   │       ├── SignalR/
│   │       │   └── ConnectionTracker.cs    — ✅ Redis sets + 24h TTL
│   │       ├── Monitoring/HealthChecks/
│   │       │   ├── DatabaseHealthCheck.cs  — ✅ IDatabasePingService abstraction
│   │       │   ├── RedisHealthCheck.cs     — ✅ IConnectionMultiplexer ping
│   │       │   └── OutboxHealthCheck.cs    — ✅ detecção de mensagens travadas
│   │       └── Outbox/
│   │           ├── OutboxMessage.cs        — ✅
│   │           └── DomainEventTypeRegistry.cs — ✅
│   └── workers/
│       └── AuctionKings.BackgroundWorkers/
│           ├── Program.cs                  — ✅ registra todos os hosted services
│           ├── OutboxProcessor.cs          — ✅ SKIP LOCKED, batch, retry (max 5)
│           └── Workers/
│               ├── AuctionCloseWorker.cs        — ✅ polling a cada 10s
│               └── CleanupExpiredDataWorker.cs  — ✅ purge hourly (expired keys + old outbox)
├── test/
│   ├── AuctionKings.UnitTests/             — ✅ Domain, Application, Shared, Infrastructure (118+ testes)
│   ├── AuctionKings.IntegrationTests/      — ✅ Testcontainers: PostgreSQL + Redis, concorrência, idempotência
│   ├── AuctionKings.ArchitectureTests/     — ⬜ Pendente (NetArchTest)
│   └── AuctionKings.LoadTests/             — ⬜ Pendente (NBomber)
├── docker/
│   ├── docker-compose.infra.yml            — ✅ PostgreSQL + Redis + LocalStack + Moto
│   ├── docker-compose.yml                  — ⬜ Vazio (API + Worker containerizados pendente)
│   ├── localstack-init/                    — ✅ scripts init para DynamoDB, SQS, SNS, S3, Secrets Manager
│   ├── moto-init.sh                        — ✅ script init Cognito, AppConfig, EventBridge
│   ├── .env                                — ✅ configurado
│   └── secrets/                            — arquivos de senha (não commitados)
├── architecture/
│   └── ARCHITECTURE.md                     — ESPECIFICAÇÃO COMPLETA v2.0
├── .github/workflows/ci.yml               — 🔄 Parcial (build + unit tests + coverage; falta arch tests e integration tests)
├── docs/                                   — vazio
├── scripts/                                — vazio
└── iac/                                    — vazio (Terraform pendente)
```

---

## 3. NuGet Packages Configurados

### `Directory.Packages.props` (implementados)

| Pacote                                          | Finalidade                    |
| ----------------------------------------------- | ----------------------------- |
| MediatR                                         | CQRS / Mediator               |
| FluentValidation.AspNetCore                     | Validação de commands         |
| Npgsql.EntityFrameworkCore.PostgreSQL           | Driver PostgreSQL             |
| Microsoft.EntityFrameworkCore.Design            | Migrations                    |
| StackExchange.Redis                             | Cliente Redis direto          |
| Microsoft.Extensions.Caching.StackExchangeRedis | IDistributedCache             |
| Microsoft.AspNetCore.SignalR.StackExchangeRedis | Redis backplane SignalR       |
| Mapster                                         | Mapeamento DTO com IRegister  |
| Testcontainers.PostgreSql                       | Integration tests             |
| Testcontainers.Redis                            | Integration tests             |
| AWSSDK.CloudWatch, AWSSDK.CloudWatchLogs        | AWS SDK                       |
| NetArchTest.Rules                               | Architecture tests (pendente) |
| coverlet.collector                              | Cobertura de testes           |
| xunit, xunit.runner.visualstudio                | Framework de testes           |

### Pacotes **ausentes** (necessários para Fase 2)

| Pacote                                        | Finalidade           |
| --------------------------------------------- | -------------------- |
| AWSSDK.DynamoDBv2                             | Event Store DynamoDB |
| Prometheus-net.AspNetCore                     | Métricas             |
| Microsoft.AspNetCore.Authentication.JwtBearer | Auth JWT (Cognito)   |

---

## 4. Infraestrutura Docker

### `docker-compose.infra.yml` — Status: COMPLETO

| Serviço        | Imagem                    | Porta | Health Check         |
| -------------- | ------------------------- | ----- | -------------------- |
| PostgreSQL 16  | postgres:16-alpine        | 5432  | pg_isready           |
| Redis 7        | redis:7-alpine            | 6379  | redis-cli ping       |
| LocalStack 4.3 | localstack/localstack:4.3 | 4566  | health endpoint      |
| Moto 5.1.4     | motoserver/moto:5.1.4     | 5555  | —                    |
| moto-init      | amazon/aws-cli            | —     | init script one-shot |

---

## 5. Endpoints HTTP

| Verbo  | Rota                          | Controlador        | Status                           |
| ------ | ----------------------------- | ------------------ | -------------------------------- |
| GET    | `/health/live`                | HealthChecks       | ✅ (liveness)                    |
| GET    | `/health/ready`               | HealthChecks       | ✅ (DB + Redis + Outbox)         |
| GET    | `/health/db`                  | HealthChecks       | ✅ (database only)               |
| GET    | `/openapi/v1.json`            | OpenAPI (dev)      | ✅                               |
| GET    | `/api/v1/auctions`            | AuctionsController | ✅ lista paginada                |
| POST   | `/api/v1/auctions`            | AuctionsController | ✅ criar leilão                  |
| GET    | `/api/v1/auctions/{id}`       | AuctionsController | ✅ detalhes                      |
| POST   | `/api/v1/auctions/{id}/close` | AuctionsController | ✅ fechar leilão                 |
| POST   | `/api/v1/auctions/{id}/bids`  | AuctionsController | ✅ submeter lance (rate limited) |
| GET    | `/api/v1/auctions/{id}/bids`  | AuctionsController | ✅ histórico de lances           |
| DELETE | `/api/v1/bids/{id}`           | BidsController     | ✅ cancelar lance                |
| GET    | `/api/v1/users/{id}/bids`     | UsersController    | ✅ histórico do usuário          |
| WS     | `/hubs/auction`               | AuctionHub         | ✅                               |
| WS     | `/hubs/notifications`         | NotificationHub    | ✅                               |

---

## 6. CI/CD

| Arquivo           | Caminho                    | Status                                                                                                              |
| ----------------- | -------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| GitHub Actions CI | `.github/workflows/ci.yml` | 🔄 Parcial — build + unit tests + coverage upload. Falta: integration tests com Docker services, architecture tests |
| GitHub Actions CD | —                          | ❌ Não implementado                                                                                                 |

---

## Resumo de Status por Camada

| Camada                    | Status                                                                     | % Completo |
| ------------------------- | -------------------------------------------------------------------------- | ---------- |
| Domain                    | ✅ Aggregates, VOs, Events, Enums, Errors                                  | ~95%       |
| Application               | ✅ CQRS, Behaviors, Commands, Queries, DTOs                                | ~90%       |
| Infrastructure            | ✅ EF Core, Redis, Outbox, Health Checks, Rate Limiter, SignalR            | ~85%       |
| API Controllers           | ✅ Todos os endpoints implementados                                        | ~85%       |
| API Hubs                  | ✅ AuctionHub + NotificationHub + ConnectionTracker                        | ~85%       |
| API Middleware/Filters    | ✅ GlobalException, SecurityHeaders, Logging, CorrelationId                | ~95%       |
| Shared Base               | ✅ Entity, AggregateRoot, Result, IDomainEvent, PagedResult                | ~100%      |
| Background Workers        | ✅ OutboxProcessor, AuctionCloseWorker, CleanupExpiredDataWorker           | ~80%       |
| Unit Tests                | ✅ 118+ testes — Domain, Application, Shared, Infrastructure               | ~85%       |
| Integration Tests         | ✅ Testcontainers — repos, cache, rate limiter, concorrência, idempotência | ~70%       |
| Architecture Tests        | ⬜ Pendente (NetArchTest)                                                  | ~0%        |
| Load Tests                | ⬜ Pendente (NBomber)                                                      | ~0%        |
| Docker Infra              | ✅ PostgreSQL + Redis + LocalStack + Moto                                  | ~95%       |
| Autenticação/Autorização  | ❌ JWT/Cognito não implementado                                            | ~0%        |
| Sagas                     | ❌ Post-MVP                                                                | ~0%        |
| Event Sourcing (DynamoDB) | ❌ Post-MVP                                                                | ~0%        |
| Métricas (Prometheus)     | ❌ Post-MVP                                                                | ~0%        |
| IaC (Terraform)           | ❌ Post-MVP                                                                | ~0%        |
