# Backlog de Tarefas — AuctionKings

> Organizado em Épicos → Histórias → Subtarefas
> Prioridades: 🔴 Crítico | 🟠 Alto | 🟡 Médio | 🟢 Baixo
> Gerado em: 2026-02-28

---

## ÉPICO 1 — Fundações: Shared + Domain (Base Classes)

> **Objetivo:** Estabelecer os blocos fundamentais que todos os outros módulos dependem.
> **Prioridade:** 🔴 Crítico — bloqueia todo o resto

---

### HIS-01 — Implementar base classes do Shared

**Prioridade:** 🔴 Crítico
**DoD:**
- `Entity<TId>` com igualdade por Id, `GetHashCode`
- `AggregateRoot` herda `Entity`, mantém lista de `IDomainEvent`, expõe `AddDomainEvent()` e `ClearDomainEvents()`
- `IDomainEvent` interface com `OccurredAt`
- `Result<T>` pattern com métodos estáticos `Success()` e `Failure()`
- `PagedResult<T>` com metadata de paginação
- `IDateTimeProvider` interface + `SystemDateTimeProvider` implementação
- `IIdempotentCommand<T>` interface com `GetIdempotencyKey()`
- Todos os tipos têm testes unitários cobrindo comportamento esperado

**Subtarefas:**
- [ ] `Shared/Common/Entity.cs` — implementar igualdade por Id
- [ ] `Shared/Common/AggregateRoot.cs` — implementar gerenciamento de DomainEvents
- [ ] `Shared/Common/IDomainEvent.cs` — criar interface
- [ ] `Shared/Common/Result.cs` — implementar Result pattern
- [ ] `Shared/Common/PagedResult.cs` — implementar paginação
- [ ] `Shared/Interfaces/IDateTimeProvider.cs` — interface + implementação concreta
- [ ] `Shared/Interfaces/IIdempotentCommand.cs` — interface
- [ ] Remover `Class1.cs` placeholders de todos os projetos
- [ ] Corrigir typo no .csproj: `Applciation` → `Application`

---

### HIS-02 — Implementar Domain Layer (Aggregates, Events, Value Objects)

**Prioridade:** 🔴 Crítico
**DoD:**
- `Auction` aggregate root com todas as regras de negócio do `PlaceBid`
- `Bid` entity criada com id, auctionId, userId, amount, requestId, createdAt
- `AuctionSnapshot` para Event Sourcing
- `AuctionStatus` e `BidStatus` enums
- Value Objects: `Money`, `AuctionDuration`, `BidIncrement`
- Todos os Domain Events definidos com propriedades imutáveis
- `DomainErrors` com todos os erros estáticos tipados
- `DomainEventTypeRegistry` para desserialização por nome
- Specifications: `ActiveAuctionSpecification`, `BiddableAuctionSpecification`
- Cobertura de testes unitários ≥ 80% para lógica de domínio

**Subtarefas:**
- [ ] `Domain/Enums/AuctionStatus.cs` — `Draft`, `Active`, `Ended`, `Cancelled`
- [ ] `Domain/Enums/BidStatus.cs` — `Active`, `Outbid`, `Won`, `Cancelled`
- [ ] `Domain/ValueObjects/Money.cs` — imutável, validação positivo
- [ ] `Domain/ValueObjects/AuctionDuration.cs`
- [ ] `Domain/ValueObjects/BidIncrement.cs`
- [ ] `Domain/Errors/DomainErrors.cs` — erros estáticos agrupados por domínio
- [ ] `Domain/Events/BidPlacedEvent.cs`
- [ ] `Domain/Events/AuctionCreatedEvent.cs`
- [ ] `Domain/Events/AuctionExtendedEvent.cs`
- [ ] `Domain/Events/AuctionClosedEvent.cs`
- [ ] `Domain/Events/WinnerDeterminedEvent.cs`
- [ ] `Domain/Aggregates/Auction/Bid.cs` — entity
- [ ] `Domain/Aggregates/Auction/Auction.cs` — aggregate root com `PlaceBid()`, `Create()`, `Close()`
- [ ] `Domain/Aggregates/Auction/AuctionSnapshot.cs`
- [ ] `Domain/Aggregates/User/User.cs`
- [ ] `Domain/Common/DomainEventTypeRegistry.cs`
- [ ] `Domain/Specifications/ActiveAuctionSpecification.cs`
- [ ] `Domain/Specifications/BiddableAuctionSpecification.cs`
- [ ] `UnitTests/Domain/AuctionTests.cs` — testar PlaceBid, regras de negócio, anti-sniping
- [ ] `UnitTests/Domain/BidTests.cs`

---

## ÉPICO 2 — Infraestrutura: Persistência (EF Core + PostgreSQL)

> **Objetivo:** Camada de persistência funcional com EF Core, Outbox Pattern e migrações.
> **Prioridade:** 🔴 Crítico — necessário para qualquer funcionalidade

---

### HIS-03 — Configurar packages e dependências

**Prioridade:** 🔴 Crítico
**DoD:**
- `Directory.Packages.props` com todos os packages necessários versionados centralmente
- Cada projeto `.csproj` referencia apenas os packages que efetivamente usa
- `dotnet restore && dotnet build` passa sem erros

**Subtarefas:**
- [ ] Adicionar ao `Directory.Packages.props`:
  - `Microsoft.AspNetCore.SignalR.StackExchangeRedis`
  - `MessagePack.AspNetCoreMvcFormatter`
  - `MediatR`
  - `Microsoft.EntityFrameworkCore`
  - `Npgsql.EntityFrameworkCore.PostgreSQL`
  - `Microsoft.EntityFrameworkCore.Design`
  - `StackExchange.Redis`
  - `Microsoft.Extensions.Caching.StackExchangeRedis`
  - `FluentValidation.AspNetCore`
  - `Microsoft.AspNetCore.Authentication.JwtBearer`
  - `AWSSDK.DynamoDBv2` (para fase 2)
  - `Prometheus-net.AspNetCore`
  - `NetArchTest.Rules` (ArchTests)
  - `Testcontainers.PostgreSql` (IntegrationTests)
  - `Testcontainers.Redis` (IntegrationTests)
- [ ] Atualizar `.csproj` de cada projeto com `<PackageReference>` corretos
- [ ] Adicionar project references (API → Application → Domain, Infrastructure → Domain)

---

### HIS-04 — Implementar DbContext e configurações EF Core

**Prioridade:** 🔴 Crítico
**DoD:**
- `AuctionDbContext` com DbSets: `Auctions`, `Bids`, `OutboxMessages`, `SagaStates`
- `AuctionConfiguration` com xmin como concurrency token PostgreSQL
- `BidConfiguration` com FK para Auction
- `OutboxMessageConfiguration`
- `SagaStateConfiguration`
- Primeira migration criada e aplicável
- Conecta ao PostgreSQL do docker-compose.infra.yml

**Subtarefas:**
- [ ] `Infrastructure/Persistence/AuctionDbContext.cs`
- [ ] `Infrastructure/Persistence/Configurations/AuctionConfiguration.cs` — incluindo `xmin` como `IsConcurrencyToken()`
- [ ] `Infrastructure/Persistence/Configurations/BidConfiguration.cs`
- [ ] `Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs`
- [ ] `Infrastructure/Persistence/Configurations/SagaStateConfiguration.cs`
- [ ] Configurar connection string em `appsettings.json` e `appsettings.Development.json`
- [ ] Criar migration inicial: `dotnet ef migrations add InitialCreate`
- [ ] `Infrastructure/Persistence/Repositories/AuctionRepository.cs`
- [ ] `Infrastructure/Persistence/Repositories/BidRepository.cs`
- [ ] Registrar DbContext no DI do `Program.cs`

---

### HIS-05 — Implementar Outbox Pattern

**Prioridade:** 🔴 Crítico
**DoD:**
- `OutboxMessage` entity com campos: Id, Type, Payload, CreatedAt, ProcessedAt, Error, RetryCount
- `UnitOfWork.SaveChangesAsync()` coleta DomainEvents dos aggregates, converte em OutboxMessages e persiste na mesma transação
- `OutboxProcessor` background service polls a cada 1 segundo com `FOR UPDATE SKIP LOCKED`
- Retry logic: máximo 5 tentativas, registra erro no OutboxMessage
- Testes de integração cobrindo fluxo completo

**Subtarefas:**
- [ ] `Infrastructure/Persistence/OutboxMessage.cs`
- [ ] `Infrastructure/Persistence/UnitOfWork.cs` — implementar lógica de coleta de eventos
- [ ] `BackgroundWorkers/Workers/OutboxProcessor.cs`
- [ ] `IEventPublisher` interface em Application
- [ ] `IntegrationTests/Infrastructure/OutboxProcessorTests.cs`

---

## ÉPICO 3 — Application Layer (CQRS + MediatR)

> **Objetivo:** Implementar comandos, queries e handlers usando MediatR com pipeline behaviors.
> **Prioridade:** 🔴 Crítico para funcionalidade core

---

### HIS-06 — Implementar PlaceBid (command + handler)

**Prioridade:** 🔴 Crítico
**DoD:**
- `PlaceBidCommand` implementa `IIdempotentCommand<Result<BidDto>>`
- `PlaceBidCommandHandler` carrega auction, chama `PlaceBid()`, trata `DbUpdateConcurrencyException`
- `PlaceBidCommandValidator` com FluentValidation (amount > 0, auctionId válido)
- `BidDto` como resultado
- Testes unitários para handler cobrindo: sucesso, leilão não encontrado, valor muito baixo, concorrência

**Subtarefas:**
- [ ] `Application/Commands/Bids/PlaceBidCommand.cs`
- [ ] `Application/Commands/Bids/PlaceBidCommandValidator.cs`
- [ ] `Application/Commands/Bids/Handlers/PlaceBidCommandHandler.cs`
- [ ] `Application/DTOs/BidDto.cs`
- [ ] `Application/Interfaces/IAuctionRepository.cs`
- [ ] `Application/Interfaces/IUnitOfWork.cs`
- [ ] `UnitTests/Application/PlaceBidCommandHandlerTests.cs`

---

### HIS-07 — Implementar CreateAuction (command + handler)

**Prioridade:** 🟠 Alto
**DoD:**
- `CreateAuctionCommand` com campos: título, descrição, preço inicial, incremento, duração, vendedor
- Handler cria aggregate, persiste, retorna `AuctionDto`
- Validator com regras de negócio
- Testes unitários

**Subtarefas:**
- [ ] `Application/Commands/Auctions/CreateAuctionCommand.cs`
- [ ] `Application/Commands/Auctions/CreateAuctionCommandValidator.cs`
- [ ] `Application/Commands/Auctions/Handlers/CreateAuctionCommandHandler.cs`
- [ ] `Application/DTOs/AuctionDto.cs`

---

### HIS-08 — Implementar Queries

**Prioridade:** 🟠 Alto
**DoD:**
- `GetAuctionDetailsQuery` retorna `AuctionDto` com bids recentes
- `GetActiveAuctionsQuery` retorna `PagedResult<AuctionDto>`
- `GetAuctionBidsQuery` retorna histórico de lances
- `GetUserBidHistoryQuery` retorna lances do usuário
- Queries leem diretamente do banco (read model, sem aggregate)

**Subtarefas:**
- [ ] `Application/Queries/Auctions/GetAuctionDetailsQuery.cs` + Handler
- [ ] `Application/Queries/Auctions/GetActiveAuctionsQuery.cs` + Handler
- [ ] `Application/Queries/Bids/GetAuctionBidsQuery.cs` + Handler
- [ ] `Application/Queries/Bids/GetUserBidHistoryQuery.cs` + Handler
- [ ] `Application/DTOs/AuctionStateDto.cs`

---

### HIS-09 — Implementar Pipeline Behaviors

**Prioridade:** 🟠 Alto
**DoD:**
- `ValidationBehavior` executa FluentValidation antes do handler, retorna erro 400 tipado
- `LoggingBehavior` loga início/fim de cada command/query com correlationId
- `IdempotencyBehavior` verifica cache distribuído antes de processar `IIdempotentCommand`
- `PerformanceBehavior` loga warning se request > 500ms
- Behaviors registrados no DI na ordem correta
- Testes unitários para `IdempotencyBehavior`

**Subtarefas:**
- [ ] `Application/Behaviors/ValidationBehavior.cs`
- [ ] `Application/Behaviors/LoggingBehavior.cs`
- [ ] `Application/Behaviors/IdempotencyBehavior.cs`
- [ ] `Application/Behaviors/PerformanceBehavior.cs`
- [ ] `Infrastructure/Caching/RedisCacheService.cs` (usado pelo IdempotencyBehavior)
- [ ] `UnitTests/Application/IdempotencyBehaviorTests.cs`

---

## ÉPICO 4 — API: Controllers, Middleware, Filters

> **Objetivo:** Expor endpoints REST versionados com autenticação, rate limiting e tratamento de erros.
> **Prioridade:** 🟠 Alto

---

### HIS-10 — Implementar AuctionsController

**Prioridade:** 🟠 Alto
**DoD:**
- Rota base: `api/v1/auctions`
- Endpoints: `GET /` (lista paginada), `POST /` (criar), `GET /{id}` (detalhes), `POST /{id}/close`, `GET /{id}/bids`
- `[RateLimit(Limit = 10, WindowSeconds = 60)]` em `POST /{id}/bids`
- Autenticação JWT requerida em todos exceto GET público
- Retorna Problem Details (RFC 7807) em erros
- Testes de integração com WebApplicationFactory

**Subtarefas:**
- [ ] `API/Controllers/V1/AuctionsController.cs` — implementar todos os endpoints
- [ ] Configurar `[ApiVersion("1.0")]` e rotas versionadas
- [ ] `API/Filters/GlobalExceptionFilter.cs` — implementar mapeamento Result → ProblemDetails
- [ ] `IntegrationTests/API/AuctionsControllerTests.cs`

---

### HIS-11 — Implementar BidsController e UsersController

**Prioridade:** 🟠 Alto
**DoD:**
- `BidsController`: `DELETE /api/v1/bids/{id}` (admin only)
- `UsersController`: `GET /api/v1/users/{id}/bids` (histórico do usuário)
- Autorização adequada por endpoint

**Subtarefas:**
- [ ] `API/Controllers/V1/BidsController.cs`
- [ ] `API/Controllers/V1/UsersController.cs` (corrigir nome)

---

### HIS-12 — Implementar Middleware e Filters

**Prioridade:** 🟡 Médio
**DoD:**
- `CorrelationIdMiddleware` — gera/propaga X-Correlation-Id em cada request
- `RequestLoggingMiddleware` — loga método, rota, status, duração
- `SecurityHeadersMiddleware` — adiciona HSTS, X-Content-Type-Options, X-Frame-Options
- `GlobalExceptionFilter` — captura exceções não tratadas, retorna Problem Details
- `IdempotencyFilter` — lê Idempotency-Key header, delega para behavior
- Todos registrados no `Program.cs` na ordem correta

**Subtarefas:**
- [ ] `API/Middleware/CorrelationIdMiddleware.cs`
- [ ] `API/Middleware/RequestLoggingMiddleware.cs`
- [ ] `API/Middleware/SecurityHeadersMiddleware.cs`
- [ ] Implementar `GlobalExceptionFilter.cs`
- [ ] Implementar `IdempotencyFilter.cs`

---

### HIS-13 — Implementar Rate Limiting Distribuído

**Prioridade:** 🟠 Alto
**DoD:**
- `RedisRateLimiter` com Lua script de sliding window
- `RateLimitAttribute` implementado como `IAsyncActionFilter`
- Headers `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `Retry-After` presentes nas respostas
- Retorna HTTP 429 quando limite excedido
- Fail-open quando Redis indisponível
- Testes unitários

**Subtarefas:**
- [ ] `Infrastructure/RateLimiting/RedisRateLimiter.cs` — implementar com Lua script
- [ ] `API/Filters/RateLimitAttribute.cs` — descomentar e implementar
- [ ] `UnitTests/Infrastructure/RedisRateLimiterTests.cs`

---

## ÉPICO 5 — Real-Time: SignalR Hubs

> **Objetivo:** Comunicação bidirecional em tempo real com reconexão e sincronização de estado.
> **Prioridade:** 🟠 Alto — feature core do produto

---

### HIS-14 — Implementar AuctionHub

**Prioridade:** 🟠 Alto
**DoD:**
- Hub requer `[Authorize]`
- `OnConnectedAsync` — rastreia conexão por userId no Redis
- `OnDisconnectedAsync` — remove conexão, sai de todos os grupos
- `JoinAuction(Guid auctionId)` — adiciona ao grupo, envia `AuctionState` atual
- `LeaveAuction(Guid auctionId)` — remove do grupo
- `RequestStateSync(Guid auctionId, int clientVersion)` — reenvia estado se version diverge
- Redis backplane configurado para escala horizontal
- MessagePack habilitado para protocolo binário

**Subtarefas:**
- [ ] `API/Hubs/AuctionHub.cs` — implementar todos os métodos
- [ ] `Infrastructure/SignalR/ConnectionTracker.cs` — rastrear conexões no Redis
- [ ] `Infrastructure/SignalR/SignalRNotificationService.cs` — implementar broadcasts
- [ ] `Infrastructure/SignalR/GracefulShutdownService.cs`
- [ ] Configurar SignalR + Redis backplane no `Program.cs`
- [ ] Mapear rotas dos hubs (`/hubs/auction`, `/hubs/notifications`)

---

### HIS-15 — Integrar broadcast de eventos com SignalR

**Prioridade:** 🟠 Alto
**DoD:**
- Quando `BidPlacedEvent` processado pelo Outbox → broadcast `BidPlaced` para grupo do leilão
- Quando `AuctionExtendedEvent` → broadcast `AuctionExtended`
- Quando `AuctionClosedEvent` → broadcast `AuctionClosed`
- `IRealtimeNotificationService` injetado nos handlers relevantes
- Testes de integração cobrindo fluxo evento → hub

**Subtarefas:**
- [ ] `Application/Interfaces/IRealtimeNotificationService.cs` — interface
- [ ] Injetar `IRealtimeNotificationService` nos event handlers do Outbox
- [ ] `API/Hubs/NotificationHub.cs` — implementar notificações por usuário

---

## ÉPICO 6 — Autenticação e Autorização

> **Objetivo:** JWT via AWS Cognito + policies customizadas.
> **Prioridade:** 🟠 Alto

---

### HIS-16 — Configurar autenticação JWT

**Prioridade:** 🟠 Alto
**DoD:**
- `AddAuthentication().AddJwtBearer()` configurado com Cognito authority
- Token lido de header `Authorization: Bearer` para REST e `X-SignalR-Token` para hubs
- `OnTokenValidated` enriquece claims com dados de perfil do usuário
- `ITokenBlacklistService` verifica JTI revogado antes de aceitar token
- `OnAuthenticationFailed` adiciona header `X-Token-Expired: true`

**Subtarefas:**
- [ ] `Infrastructure/Security/TokenBlacklistService.cs` — implementar com Redis
- [ ] Configurar JWT no `Program.cs` com eventos customizados
- [ ] `appsettings.json` com seção `AWS:Cognito` (sem secrets, apenas configuração)

---

### HIS-17 — Implementar Authorization Policies

**Prioridade:** 🟡 Médio
**DoD:**
- Policies: `VerifiedEmail`, `Seller`, `Admin`, `CanBid`, `CanCreateAuction`, `HighValueBid`
- `CanBidAuthorizationHandler` — verifica email verificado + reputação mínima
- `HighValueBidAuthorizationHandler` — requer MFA ou KYC para lances > R$10.000
- Todos os handlers registrados no DI

**Subtarefas:**
- [ ] `Infrastructure/Authorization/CanBidAuthorizationHandler.cs`
- [ ] `Infrastructure/Authorization/CanCreateAuctionAuthorizationHandler.cs`
- [ ] `Infrastructure/Authorization/HighValueBidAuthorizationHandler.cs`
- [ ] Registrar policies e handlers no `Program.cs`

---

## ÉPICO 7 — Background Workers

> **Objetivo:** Workers para encerramento automático de leilões e processamento assíncrono.
> **Prioridade:** 🟠 Alto para AuctionCloseWorker, 🟡 Médio para outros

---

### HIS-18 — Implementar AuctionCloseWorker

**Prioridade:** 🟠 Alto
**DoD:**
- Worker consulta leilões com `Status = Active AND EndTime <= UtcNow` periodicamente
- Enfileira `CloseAuctionCommand` via SQS (ou processa diretamente em dev)
- Distributed lock com Redis para evitar processamento duplo
- Trata `CloseAuctionCommand` com `CloseAuctionCommandHandler`
- Dispara `AuctionClosingStartedEvent` para iniciar Saga

**Subtarefas:**
- [ ] `Application/Commands/Auctions/CloseAuctionCommand.cs` + Handler
- [ ] `BackgroundWorkers/Workers/AuctionCloseWorker.cs`
- [ ] Configurar worker no `BackgroundWorkers/Program.cs`

---

### HIS-19 — Implementar CleanupExpiredDataJob e outros Jobs

**Prioridade:** 🟢 Baixo
**DoD:**
- `CleanupExpiredDataJob` — limpa OutboxMessages processados com mais de 30 dias
- `SnapshotCreationJob` — força snapshot de aggregates em streams com muitos eventos
- Schedulados com Quartz.NET ou implementação simples com `PeriodicTimer`

**Subtarefas:**
- [ ] `BackgroundWorkers/Jobs/CleanupExpiredDataJob.cs`
- [ ] `BackgroundWorkers/Jobs/SnapshotCreationJob.cs`
- [ ] `BackgroundWorkers/Jobs/ReportGenerationJob.cs`

---

## ÉPICO 8 — Saga: AuctionClosing

> **Objetivo:** Coordenar encerramento distribuído com compensação.
> **Prioridade:** 🟡 Médio

---

### HIS-20 — Implementar AuctionClosingSaga

**Prioridade:** 🟡 Médio
**DoD:**
- `AuctionClosingSagaState` com todos os campos de estado e `SagaStatus` enum
- `ISagaStateRepository` com `SaveAsync` e `GetByIdAsync`
- `AuctionClosingSagaHandler` com os 4 steps: DetermineWinner, ReservePayment, ReserveItem, NotifyParticipants
- Compensação em ordem reversa para cada step que falhou
- Estado persistido em PostgreSQL a cada step
- Evento publicado no sucesso/falha

**Subtarefas:**
- [ ] `Domain/Sagas/AuctionClosingSagaState.cs`
- [ ] `Application/Sagas/ISagaStateRepository.cs`
- [ ] `Application/Sagas/AuctionClosingSagaHandler.cs`
- [ ] `Infrastructure/Persistence/Repositories/SagaStateRepository.cs`
- [ ] Interfaces stub para `IPaymentService`, `IInventoryService`, `INotificationService`

---

## ÉPICO 9 — Event Sourcing (DynamoDB)

> **Objetivo:** Implementar store de eventos com snapshots para reconstituição de aggregates.
> **Prioridade:** 🟡 Médio (pode usar EF Core como fallback temporariamente)

---

### HIS-21 — Implementar IEventStore com DynamoDB

**Prioridade:** 🟡 Médio
**DoD:**
- `IEventStore` interface com `AppendEvents`, `GetEvents`, `SaveSnapshot`, `GetLatestSnapshot`
- `DynamoDbEventStore` com conditional writes para concorrência
- Snapshot a cada 50 eventos
- `DomainEventTypeRegistry` para desserialização por nome
- `EventSourcedAuctionRepository` carrega do snapshot + eventos incrementais
- Tabelas DynamoDB criadas via Terraform ou script

**Subtarefas:**
- [ ] `Application/Interfaces/IEventStore.cs`
- [ ] `Infrastructure/EventStore/DynamoDbEventStore.cs`
- [ ] `Infrastructure/EventStore/EventSourcedAuctionRepository.cs`
- [ ] `Infrastructure/EventStore/ConcurrencyException.cs`
- [ ] `Domain/Common/DomainEventTypeRegistry.cs`

---

## ÉPICO 10 — Health Checks e Observabilidade

> **Objetivo:** Monitoramento da saúde do sistema e métricas de negócio.
> **Prioridade:** 🟡 Médio

---

### HIS-22 — Implementar Health Checks customizados

**Prioridade:** 🟡 Médio
**DoD:**
- `DatabaseHealthCheck` verifica conectividade PostgreSQL com query simples
- `RedisHealthCheck` verifica ping ao Redis
- `SignalRHealthCheck` verifica estado do hub
- Endpoint `/health/ready` (todos checks) e `/health/live` (apenas alive)
- Resposta JSON com status por componente

**Subtarefas:**
- [ ] `Infrastructure/Monitoring/HealthChecks/DatabaseHealthCheck.cs`
- [ ] `Infrastructure/Monitoring/HealthChecks/RedisHealthCheck.cs`
- [ ] `Infrastructure/Monitoring/HealthChecks/SignalRHealthCheck.cs`
- [ ] Configurar `/health/ready` e `/health/live` no `Program.cs`

---

### HIS-23 — Implementar métricas Prometheus

**Prioridade:** 🟢 Baixo
**DoD:**
- Métricas de negócio: lances/segundo, leilões ativos, usuários conectados
- Métricas técnicas: latência de endpoints, erros de concorrência, tamanho da fila outbox
- Endpoint `/metrics` disponível para scraping

**Subtarefas:**
- [ ] `Infrastructure/Monitoring/PrometheusMetrics.cs`
- [ ] Configurar prometheus-net no `Program.cs`
- [ ] Instrumentar `PlaceBidCommandHandler` com counters/histogramas

---

## ÉPICO 11 — Tests

> **Objetivo:** Cobertura adequada em todos os níveis.
> **Prioridade:** 🟠 Alto (deve ser feito junto com cada feature)

---

### HIS-24 — Testes Unitários do Domínio

**Prioridade:** 🟠 Alto
**DoD:**
- Cobertura ≥ 80% dos métodos de `Auction` aggregate
- Cenários: lance válido, leilão encerrado, valor abaixo do atual, usuário já vencendo, anti-sniping, concorrência
- Testes para todos os Value Objects
- Testes para Result pattern

**Subtarefas:**
- [ ] `UnitTests/Domain/AuctionTests.cs`
- [ ] `UnitTests/Domain/BidTests.cs`
- [ ] `UnitTests/Domain/ValueObjectTests.cs`

---

### HIS-25 — Testes de Arquitetura

**Prioridade:** 🟡 Médio
**DoD:**
- `DependencyTests` verifica que Domain não referencia Infrastructure/Application
- `DependencyTests` verifica que Application não referencia Infrastructure (apenas interfaces)
- `NamingConventionTests` verifica que todos os handlers terminam em "Handler", commands em "Command", etc.
- Usa NetArchTest.Rules

**Subtarefas:**
- [ ] Adicionar `NetArchTest.Rules` ao `Directory.Packages.props`
- [ ] `ArchitectureTests/DependencyTests.cs`
- [ ] `ArchitectureTests/NamingConventionTests.cs`

---

### HIS-26 — Testes de Integração com Testcontainers

**Prioridade:** 🟡 Médio
**DoD:**
- `PostgresContainerFixture` sobe PostgreSQL via Testcontainers, aplica migrations
- `RedisFixture` sobe Redis via Testcontainers
- `AuctionsControllerTests` testa endpoints completos via WebApplicationFactory
- `OutboxProcessorTests` testa fluxo completo de publicação de eventos
- `RepositoryTests` testa CRUD contra banco real

**Subtarefas:**
- [ ] Adicionar `Testcontainers.PostgreSql` e `Testcontainers.Redis`
- [ ] `IntegrationTests/TestContainers/PostgresContainerFixture.cs`
- [ ] `IntegrationTests/Fixtures/RedisFixture.cs`
- [ ] `IntegrationTests/API/AuctionsControllerTests.cs`
- [ ] `IntegrationTests/Infrastructure/OutboxProcessorTests.cs`
- [ ] `IntegrationTests/Infrastructure/RepositoryTests.cs`

---

## ÉPICO 12 — Docker e Deploy

> **Objetivo:** Containerização completa e pipeline CI/CD.
> **Prioridade:** 🟢 Baixo (pode ser feito após MVP funcional)

---

### HIS-27 — Dockerfiles e docker-compose completo

**Prioridade:** 🟢 Baixo
**DoD:**
- `Dockerfile.api` com multi-stage build
- `Dockerfile.worker` com multi-stage build
- `docker-compose.yml` com API + Worker + dependência na infra
- `docker-compose.dev.yml` com hot-reload e volumes
- `docker compose up` inicia todo o sistema localmente

**Subtarefas:**
- [ ] `docker/Dockerfile.api`
- [ ] `docker/Dockerfile.worker`
- [ ] `docker/docker-compose.yml` — API + Worker services
- [ ] `docker/docker-compose.dev.yml`

---

### HIS-28 — Terraform e IaC

**Prioridade:** 🟢 Baixo
**DoD:**
- Módulos Terraform para: VPC, ECS Fargate, RDS PostgreSQL, ElastiCache Redis, DynamoDB, Cognito
- Ambientes: dev, staging, prod
- Secrets no AWS Secrets Manager

**Subtarefas:**
- [ ] `iac/terraform/modules/vpc/`
- [ ] `iac/terraform/modules/ecs/`
- [ ] `iac/terraform/modules/rds/`
- [ ] `iac/terraform/modules/elasticache/`
- [ ] `iac/terraform/modules/dynamodb/`
- [ ] `iac/terraform/modules/cognito/`
- [ ] `iac/terraform/environments/dev/`

---

## ÉPICO 13 — Correções e Dívida Técnica

> **Prioridade:** 🟠 Alto — deve ser feito antes de qualquer PR

---

### HIS-29 — Corrigir problemas existentes

**Prioridade:** 🟠 Alto
**DoD:**
- Typo no .csproj corrigido: `Applciation` → `Application`
- `UsersControllers.cs` renomeado para `UsersController.cs`
- Todos os `Class1.cs` placeholder removidos
- Rotas dos controllers atualizadas para padrão versionado `api/v{version:apiVersion}/`
- `RateLimitAttribute` descomentado

**Subtarefas:**
- [ ] Renomear `AuctionKings.Applciation.csproj`
- [ ] Renomear `UsersControllers.cs` → `UsersController.cs` e atualizar classe
- [ ] Remover `Class1.cs` de todos os projetos
- [ ] Atualizar rotas nos controllers para versioning
