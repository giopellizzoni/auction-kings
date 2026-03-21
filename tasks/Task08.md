# Task 08 — Implementar Redis: Cache Service e Rate Limiter

## Contexto

Redis serve dois propósitos principais: cache distribuído para queries frequentes (listagem de leilões, detalhes de leilão) e rate limiting para proteger endpoints críticos (PlaceBid). Também será backplane do SignalR (Task 09). A implementação deve usar `IDistributedCache` do ASP.NET Core para cache e acesso direto ao `IConnectionMultiplexer` para rate limiting com scripts Lua.

## Arquivos a Alterar/Criar

- `src/services/AuctionKings.Infrastructure/AuctionKings.Infrastructure.csproj` — adicionar `Microsoft.Extensions.Caching.StackExchangeRedis`, `StackExchange.Redis`
- `Directory.Packages.props` — adicionar versões
- `src/services/AuctionKings.Infrastructure/Caching/RedisCacheService.cs` — implementa `ICacheService` com `GetAsync<T>`, `SetAsync<T>`, `RemoveAsync`, `RemoveByPrefixAsync`
- `src/services/AuctionKings.Infrastructure/Caching/CacheKeys.cs` — classe estática com patterns de keys: `auction:{id}`, `auctions:active:page:{page}`, etc.
- `src/services/AuctionKings.Infrastructure/Caching/CacheInvalidationService.cs` — invalida cache relacionado quando auction é modificada
- `src/services/AuctionKings.Infrastructure/RateLimiting/RedisRateLimiter.cs` — sliding window rate limiter usando script Lua atomico
- `src/services/AuctionKings.Infrastructure/DependencyInjection.cs` — atualizar para registrar Redis cache e rate limiter
- `src/services/AuctionKings.API/Filters/RateLimitAttribute.cs` — implementar o filter (atualmente comentado) usando `RedisRateLimiter`

## Critérios de Aceite

- `RedisCacheService` serializa/deserializa com `System.Text.Json`, suporta TTL configurável por tipo de dado
- `RemoveByPrefixAsync` usa `SCAN` (não `KEYS`) para ser production-safe
- `RedisRateLimiter` usa sliding window: configuras requests/window (ex: 10 bids por minuto por usuário)
- `RateLimitAttribute` retorna `429 Too Many Requests` com header `Retry-After`
- Cache invalidation é chamado no `UnitOfWork` ou nos command handlers após mutações
- Fallback gracioso se Redis estiver indisponível (log warning, bypass cache)

## Testes Obrigatórios

- `RedisCacheService_SetAndGet_ShouldWork` (integration com Testcontainers Redis)
- `RedisCacheService_Expired_ShouldReturnNull`
- `RedisCacheService_RemoveByPrefix_ShouldRemoveMatchingKeys`
- `RedisRateLimiter_WithinLimit_ShouldAllow`
- `RedisRateLimiter_ExceedLimit_ShouldBlock`
- `CacheInvalidationService_AfterAuctionUpdate_ShouldInvalidateRelatedKeys`

## Riscos/Observações

- Scripts Lua no Redis são atômicos mas não devem ser longos (< 5ms)
- `SCAN` com pattern pode ser lento em databases muito grandes — considerar sets para tracking de keys por prefix
- Connection string do Redis deve suportar senha via Docker secrets
- TTLs sugeridos: listagem = 30s, detalhes = 60s, contadores = 5s

## Estimativa

**M** (Medium)

## Dependências

- **Task 03** — interface `ICacheService`
- **Task 06** — `DependencyInjection.cs` da infra (para adicionar registros)

## Status

✅ **Completed** (merged via PR [#81](https://github.com/PellizzoniCode/auction-kings/pull/81))

## GitHub Issues

| # | Title | Status |
|---|-------|--------|
| [#46](https://github.com/PellizzoniCode/auction-kings/issues/46) | Configurar Redis backplane para SignalR (dev) | 🔴 open |
| [#51](https://github.com/PellizzoniCode/auction-kings/issues/51) | Rate limiting básico (in-memory) para endpoints críticos | 🔴 open |
