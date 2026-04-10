# Ordem de Execução — AuctionKings

> Sequência de implementação baseada em dependências técnicas e valor entregue por sprint.
> Atualizado em: 2026-04-10

---

## Princípios da Ordem

1. **Dependências primeiro:** Domain antes de Application, Application antes de Infrastructure, Infrastructure antes de API
2. **Vertical slicing:** implementar um fluxo de ponta a ponta antes de adicionar mais features
3. **Testes junto:** cada história inclui seus testes
4. **Infra local primeiro:** PostgreSQL + Redis locais (docker) antes de integrar AWS

---

## Estado Atual dos Sprints

```
Sprint 0  │ HIS-29  HIS-03            ✅ CONCLUÍDO
          │ (packages + setup)
──────────┤
Sprint 1  │ HIS-01  HIS-02  HIS-24    ✅ CONCLUÍDO
          │ (Shared + Domain + Unit Tests)
──────────┤
Sprint 2  │ HIS-04  HIS-05  HIS-06  HIS-09   ✅ CONCLUÍDO
          │ (EF Core + Outbox + PlaceBid + Behaviors)
──────────┤
Sprint 3  │ HIS-07  HIS-08  HIS-13  HIS-10  HIS-11   ✅ CONCLUÍDO
          │ (Commands/Queries + Rate Limit + Controllers)
──────────┤
Sprint 4  │ HIS-14  HIS-15  HIS-12    ✅ CONCLUÍDO
          │ (SignalR Hubs + Broadcast + Middleware)
──────────┤
Sprint 5  │ HIS-16  HIS-17            ❌ PENDENTE
          │ (JWT Auth + Cognito Policies)
──────────┤
Sprint 6  │ HIS-18  HIS-19  HIS-22   ✅ CONCLUÍDO
          │ (Workers + Health Checks)
──────────┤
Sprint 7  │ HIS-25  HIS-26  HIS-27   🔄 EM PROGRESSO
          │ (Arch Tests + Integration Tests + Docker)
──────────┤
[MVP ✅ funcional — falta auth para prod]
──────────┤
Fase 2    │ HIS-20  HIS-21  HIS-23  HIS-19(parcial)  HIS-28
          │ (Saga + DynamoDB + Prometheus + IaC)
```

---

## Sprint 0 — Pré-requisitos e Configuração ✅ CONCLUÍDO

| Ordem | História | Descrição | Status |
|-------|----------|-----------|--------|
| 1 | **HIS-29** | Packages NuGet, project references, typos | ✅ |
| 2 | **HIS-03** | Directory.Packages.props completo | ✅ |

---

## Sprint 1 — Fundações do Domínio ✅ CONCLUÍDO

| Ordem | História | Descrição | Status |
|-------|----------|-----------|--------|
| 1 | **HIS-01** | Entity, AggregateRoot, Result, IDomainEvent, PagedResult | ✅ |
| 2 | **HIS-02** | Domain: Auction, Bid, VOs, Events, Errors, Enums | ✅ |
| 3 | **HIS-24** | Unit tests do domínio (> 80% cobertura) | ✅ |

---

## Sprint 2 — Persistência e CQRS Core ✅ CONCLUÍDO

| Ordem | História | Descrição | Status |
|-------|----------|-----------|--------|
| 1 | **HIS-04** | EF Core DbContext, configurações, migration InitialCreate | ✅ |
| 2 | **HIS-05** | Outbox Pattern: UnitOfWork, OutboxProcessor (SKIP LOCKED) | ✅ |
| 3 | **HIS-06** | PlaceBidCommand + Handler + Validator | ✅ |
| 4 | **HIS-09** | Pipeline Behaviors: Validation, Logging, Idempotency, Performance | ✅ |

---

## Sprint 3 — API REST e Rate Limiting ✅ CONCLUÍDO

| Ordem | História | Descrição | Status |
|-------|----------|-----------|--------|
| 1 | **HIS-08** | Queries: GetAuctionDetails, GetActiveAuctions (cache), GetAuctionBids | ✅ |
| 2 | **HIS-07** | CreateAuctionCommand + Handler | ✅ |
| 3 | **HIS-13** | RedisRateLimiter (Lua) + RateLimitAttribute | ✅ |
| 4 | **HIS-10** | AuctionsController com todos os endpoints + Swagger | ✅ |
| 5 | **HIS-11** | BidsController e UsersController | ✅ |

---

## Sprint 4 — Real-Time com SignalR ✅ CONCLUÍDO

| Ordem | História | Descrição | Status |
|-------|----------|-----------|--------|
| 1 | **HIS-14** | AuctionHub + ConnectionTracker + Redis backplane | ✅ |
| 2 | **HIS-15** | Broadcast BidPlaced / AuctionExtended / AuctionClosed | ✅ |
| 3 | **HIS-12** | Middleware: CorrelationId, RequestLogging, SecurityHeaders | ✅ |

---

## Sprint 5 — Autenticação e Autorização ❌ PENDENTE

> **Bloqueante para produção.** Todos os endpoints atualmente públicos.

| Ordem | História | Descrição | Status |
|-------|----------|-----------|--------|
| 1 | **HIS-16** | JWT authentication com Cognito (ou mock local) | ❌ |
| 2 | **HIS-17** | Authorization policies: CanBid, HighValueBid, Seller, Admin | ❌ |

**Notas:**
- Para desenvolvimento local, implementar dummy auth middleware que gera tokens fixos antes de integrar Cognito real.
- `LocalStack` + `Moto` já configurados — Cognito emulado via Moto disponível na porta 5555.
- `appsettings.LocalStack.json` com endpoint overrides já existe.

---

## Sprint 6 — Workers e Saúde do Sistema ✅ CONCLUÍDO

| Ordem | História | Descrição | Status |
|-------|----------|-----------|--------|
| 1 | **HIS-18** | AuctionCloseWorker (polling 10s, fecha leilões expirados) | ✅ |
| 2 | **HIS-19** | CleanupExpiredDataWorker (purge hourly) | ✅ |
| 3 | **HIS-22** | Health Checks: DatabaseHealthCheck, RedisHealthCheck, OutboxHealthCheck | ✅ |

---

## Sprint 7 — Qualidade e Empacotamento 🔄 EM PROGRESSO

| Ordem | História | Descrição | Status |
|-------|----------|-----------|--------|
| 1 | **HIS-25** | Architecture Tests com NetArchTest | ⬜ Pendente |
| 2 | **HIS-26** | Integration Tests — AuctionsControllerTests end-to-end | 🔄 Parcial (repos/cache/rate limiter prontos; controller tests pendentes) |
| 3 | **HIS-27** | Dockerfiles + docker-compose.yml para API + Worker | ❌ |
| 4 | **HIS-29-CI** | GitHub Actions CI completo (+ integration tests + arch tests) | 🔄 Parcial |

**Critério de conclusão do Sprint 7:** `dotnet test` passa em todos os projetos incluindo Architecture Tests. `docker compose up` inicia API + Worker + PostgreSQL + Redis.

---

## Fase 2 — Funcionalidades Avançadas (pós-MVP)

| Prioridade | História | Descrição |
|-----------|----------|-----------|
| 🟡 Médio | **HIS-20** | Saga AuctionClosing com compensação |
| 🟡 Médio | **HIS-21** | Event Store com DynamoDB (LocalStack disponível) |
| 🟡 Médio | **HIS-23** | Métricas Prometheus + Grafana |
| 🟢 Baixo | **HIS-19b** | SnapshotCreationJob, ReportGenerationJob |
| 🟢 Baixo | **HIS-28** | Terraform / IaC AWS |
| 🟢 Baixo | **T18** | Load Tests com NBomber |

---

## Pontos de Atenção

1. **Auth em dev:** `Moto` já está configurado e emula Cognito localmente (porta 5555). Ao implementar HIS-16, usar `appsettings.LocalStack.json` para apontar para Moto antes de integrar Cognito real.

2. **SQS/SNS:** OutboxProcessor usa MediatR in-memory. Issue #38 rastreia a migração para SQS. LocalStack já tem SQS/SNS inicializados pelos scripts de init.

3. **DynamoDB Event Store:** É opcional para o MVP. EF Core/PostgreSQL é suficiente até que Event Sourcing seja necessário. LocalStack já tem tabelas DynamoDB configuradas.

4. **Architecture Tests:** Prioridade alta para Sprint 7 — garante que as regras de Clean Architecture não são violadas conforme o projeto cresce.

5. **Load Tests:** Executar apenas após MVP completo + auth implementado, em ambiente isolado.
