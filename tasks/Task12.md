# Task 12 — Implementar Health Checks Completos

## Contexto

O `Program.cs` atual registra `AddHealthChecks()` e mapeia apenas `/health/live` sem checks reais. A spec define três endpoints de health: `/health/live` (liveness — sempre 200), `/health/ready` (readiness — verifica PostgreSQL e Redis), `/health/db` (database-specific). Health checks são essenciais para orquestração de containers (Kubernetes/ECS).

## Arquivos a Alterar/Criar

- `src/services/AuctionKings.Infrastructure/HealthChecks/DatabaseHealthCheck.cs` — `IHealthCheck` que executa `SELECT 1` no PostgreSQL
- `src/services/AuctionKings.Infrastructure/HealthChecks/RedisHealthCheck.cs` — `IHealthCheck` que executa `PING` no Redis
- `src/services/AuctionKings.Infrastructure/HealthChecks/OutboxHealthCheck.cs` — verifica se não há mensagens stuck (não processadas há > X minutos)
- `src/services/AuctionKings.API/Program.cs` — registrar health checks com tags (`live`, `ready`, `db`), mapear endpoints com `ResponseWriter` que retorna JSON detalhado
- `src/services/AuctionKings.Infrastructure/DependencyInjection.cs` — registrar health checks no DI

## Critérios de Aceite

- `/health/live` retorna 200 sempre (apenas confirma que o processo está rodando)
- `/health/ready` executa checks de PostgreSQL e Redis, retorna 200 se ambos ok, 503 se algum falhar
- Response em JSON com status individual de cada check, duração e mensagem de erro se aplicável
- `OutboxHealthCheck` retorna `Degraded` se há mensagens não processadas > 5 minutos
- Health checks têm timeout individual de 5 segundos
- Compatível com Docker HEALTHCHECK e load balancer probes

## Testes Obrigatórios

- `DatabaseHealthCheck_PostgresAvailable_ShouldReturnHealthy`
- `DatabaseHealthCheck_PostgresDown_ShouldReturnUnhealthy`
- `RedisHealthCheck_RedisAvailable_ShouldReturnHealthy`
- `RedisHealthCheck_RedisDown_ShouldReturnUnhealthy`
- `OutboxHealthCheck_NoStuckMessages_ShouldReturnHealthy`
- `OutboxHealthCheck_StuckMessages_ShouldReturnDegraded`

## Riscos/Observações

- Health check de database não deve usar a connection pool principal — usar connection dedicada ou curta
- Considerar usar `AspNetCore.HealthChecks.NpgSql` e `AspNetCore.HealthChecks.Redis` do pacote `AspNetCore.Diagnostics.HealthChecks` ao invés de implementar do zero
- Response writer customizado para JSON com `System.Text.Json`
- Em produção, `/health/live` e `/health/ready` devem ser públicos (sem auth), mas não expor detalhes sensíveis

## Estimativa

**S** (Small)

## Dependências

- **Task 06** — DbContext disponível (para database health check)
- **Task 08** — Redis configurado (para Redis health check)

## Status

✅ **Completed** — DatabaseHealthCheck, RedisHealthCheck, and OutboxHealthCheck all implemented in Infrastructure.

## GitHub Issues

| #                                                                | Title                                     | Status    |
| ---------------------------------------------------------------- | ----------------------------------------- | --------- |
| [#8](https://github.com/PellizzoniCode/auction-kings/issues/8)   | Configurar HealthCheck básico (/health)   | ✅ closed |
| [#56](https://github.com/PellizzoniCode/auction-kings/issues/56) | Health checks: DB, Redis, OutboxProcessor | ✅ closed |
