# Task 15 — Implementar Integration Tests com Testcontainers

## Contexto

Integration tests verificam o funcionamento end-to-end das camadas juntas: API → Application → Infrastructure → Database real. Usam `Testcontainers` para levantar PostgreSQL e Redis em containers Docker durante os testes, garantindo que EF Core migrations, queries SQL e cache funcionam corretamente. O projeto `AuctionKings.IntegrationTests` contém apenas um teste placeholder.

## Arquivos a Alterar/Criar

- `test/AuctionKings.IntegrationTests/AuctionKings.IntegrationTests.csproj` — adicionar: `Testcontainers.PostgreSql`, `Testcontainers.Redis`, `Microsoft.AspNetCore.Mvc.Testing`, project refs para API, Infrastructure
- `Directory.Packages.props` — adicionar versões de `Testcontainers`, `Testcontainers.PostgreSql`, `Testcontainers.Redis`
- `test/AuctionKings.IntegrationTests/Fixtures/PostgresFixture.cs` — `IAsyncLifetime` fixture que levanta PostgreSQL container, aplica migrations
- `test/AuctionKings.IntegrationTests/Fixtures/RedisFixture.cs` — `IAsyncLifetime` fixture que levanta Redis container
- `test/AuctionKings.IntegrationTests/Fixtures/IntegrationTestWebAppFactory.cs` — `WebApplicationFactory<Program>` customizada que substitui connection strings pelos containers
- `test/AuctionKings.IntegrationTests/Controllers/AuctionsControllerTests.cs` — testes HTTP end-to-end: criar leilão, listar, buscar por ID
- `test/AuctionKings.IntegrationTests/Controllers/BidsControllerTests.cs` — testes HTTP: fazer lance, listar bids
- `test/AuctionKings.IntegrationTests/Repositories/AuctionRepositoryTests.cs` — testes contra PostgreSQL real
- `test/AuctionKings.IntegrationTests/Persistence/OutboxProcessorTests.cs` — verifica que outbox messages são processadas corretamente
- `test/AuctionKings.IntegrationTests/Persistence/ConcurrencyTests.cs` — verifica optimistic concurrency com `xmin`
- Remover `test/AuctionKings.IntegrationTests/UnitTest1.cs`

## Critérios de Aceite

- Testes levantam containers reais via Testcontainers (PostgreSQL 16, Redis 7)
- Fixtures aplicam EF Core migrations automaticamente no container
- `WebApplicationFactory` substitui connection strings para apontar aos containers
- Testes de API fazem requests HTTP reais via `HttpClient` do WebAppFactory
- Testes de concurrency provam que two updates simultâneos no mesmo aggregate geram `DbUpdateConcurrencyException`
- Containers são compartilhados entre testes da mesma collection (performance)
- Todos os testes passam com `dotnet test test/AuctionKings.IntegrationTests/`
- Testes rodam em < 60 segundos (containers são reusados)

## Testes Obrigatórios

- `AuctionsController_CreateAndGet_ShouldReturnCreatedAuction`
- `AuctionsController_GetActiveAuctions_ShouldReturnPaginated`
- `BidsController_PlaceBid_ShouldPersistAndReturn201`
- `BidsController_PlaceBid_ConcurrentBids_OnlyHighestWins`
- `AuctionRepository_ConcurrentUpdate_ShouldThrowConcurrencyException`
- `OutboxProcessor_ShouldProcessAndMarkAsProcessed`
- `CacheService_SetAndGet_WithRealRedis_ShouldWork`

## Riscos/Observações

- Docker precisa estar rodando na máquina de CI — verificar configuração do CI runner
- Testcontainers pode ser lento na primeira execução (pull de imagens) — cache de imagens no CI
- Collection fixtures do xUnit para compartilhar containers entre test classes
- Cleanup entre testes: truncar tabelas ou usar transactions com rollback
- Testes de integração são mais lentos — não duplicar o que já está coberto em unit tests

## Estimativa

**L** (Large)

## Dependências

- **Task 06** — EF Core DbContext, migrations, repositories
- **Task 07** — Outbox processor
- **Task 08** — Redis cache service
- **Task 10** — controllers implementados
- **Task 14** — unit tests (para evitar duplicação de cobertura)

## Status

✅ **Completed** — Testcontainers fixtures (PostgreSqlFixture, RedisFixture) implemented; AuctionRepository, BidRepository, AuctionConfiguration, UnitOfWork, RedisCacheService, CacheInvalidationService, and RedisRateLimiter integration tests done. PlaceBidConcurrencyTests and PlaceBidIdempotencyTests implemented.

## GitHub Issues

| #                                                                | Title                                                | Status     |
| ---------------------------------------------------------------- | ---------------------------------------------------- | ---------- |
| [#39](https://github.com/PellizzoniCode/auction-kings/issues/39) | Integration test: concorrência de lances (n threads) | ✅ closed  |
| [#40](https://github.com/PellizzoniCode/auction-kings/issues/40) | Integration test: idempotência (mesmo X-Request-Id)  | ✅ closed  |
| [#60](https://github.com/PellizzoniCode/auction-kings/issues/60) | Configurar Testcontainers para Postgres e Redis      | ✅ closed  |
