# Ordem de Execução Recomendada — AuctionKings

> Sequência de implementação baseada em dependências técnicas e valor entregue por sprint.
> Gerado em: 2026-02-28

---

## Princípios da Ordem

1. **Dependências primeiro:** Domain antes de Application, Application antes de Infrastructure, Infrastructure antes de API
2. **Vertical slicing:** prefira implementar um fluxo de ponta a ponta (ex: PlaceBid completo) antes de adicionar mais features
3. **Testes junto:** cada história inclui seus testes — não deixar para o fim
4. **Infra local primeiro:** usar PostgreSQL + Redis locais (docker) antes de integrar AWS

---

## Diagrama de Dependências

```
HIS-03 (packages)
    ↓
HIS-01 (Shared base)
    ↓
HIS-02 (Domain)
    ↓
HIS-04 (EF Core + DbContext)     ←── HIS-29 (correções)
    ↓
HIS-05 (Outbox Pattern)
    ↓
HIS-06 (PlaceBid command)
    ↓
HIS-09 (Pipeline Behaviors)      ←── HIS-08 (Queries)
    ↓
HIS-10 (AuctionsController)      ←── HIS-13 (Rate Limiting)
    ↓
HIS-14 (AuctionHub SignalR)      ←── HIS-15 (Broadcast eventos)
    ↓
HIS-16 (Auth JWT)                ←── HIS-17 (Auth Policies)
    ↓
HIS-18 (AuctionCloseWorker)
    ↓
HIS-22 (Health Checks)
    ↓
[MVP Funcional — Fase 1 concluída]
    ↓
HIS-07 (CreateAuction)
HIS-11 (Bids/Users Controllers)
HIS-12 (Middleware)
HIS-20 (Saga)
HIS-21 (Event Sourcing DynamoDB)
HIS-23 (Prometheus)
HIS-24 (Unit Tests)
HIS-25 (Arch Tests)
HIS-26 (Integration Tests)
HIS-27 (Docker compose completo)
HIS-28 (Terraform)
HIS-19 (Cleanup Jobs)
```

---

## Sprint 0 — Pré-requisitos e Correções

**Duração:** 1 dia
**Objetivo:** Ambiente limpo, sem dívida técnica inicial

| Ordem | História | Descrição |
|-------|----------|-----------|
| 1 | **HIS-29** | Corrigir typo no .csproj, renomear UsersControllers, remover Class1.cs |
| 2 | **HIS-03** | Adicionar todos os NuGet packages no `Directory.Packages.props` e atualizar .csproj |

**Critério de conclusão:** `dotnet restore && dotnet build` passa sem erros ou warnings.

---

## Sprint 1 — Fundações do Domínio

**Duração:** 2–3 dias
**Objetivo:** Domínio implementado e testado. Nenhum acoplamento externo.

| Ordem | História | Descrição | Dependência |
|-------|----------|-----------|-------------|
| 1 | **HIS-01** | Shared base classes: Entity, AggregateRoot, Result, IDomainEvent | Sprint 0 |
| 2 | **HIS-02** | Domain: Aggregates, Events, Value Objects, Errors, Enums, Specs | HIS-01 |
| 3 | **HIS-24** | Testes unitários de domínio | HIS-02 |

**Critério de conclusão:** `dotnet test test/AuctionKings.UnitTests/` passa com cobertura ≥ 80% no domínio.

---

## Sprint 2 — Persistência e CQRS Core

**Duração:** 3–4 dias
**Objetivo:** PlaceBid de ponta a ponta com persistência real.

| Ordem | História | Descrição | Dependência |
|-------|----------|-----------|-------------|
| 1 | **HIS-04** | EF Core DbContext, configurações, migration inicial | Sprint 1 |
| 2 | **HIS-05** | Outbox Pattern: OutboxMessage, UnitOfWork, OutboxProcessor | HIS-04 |
| 3 | **HIS-06** | PlaceBidCommand + Handler + Validator + BidDto | HIS-04, HIS-02 |
| 4 | **HIS-09** | Pipeline Behaviors: Validation, Logging, Idempotency, Performance | HIS-06 |

**Critério de conclusão:** `PlaceBidCommandHandler` processa lance, persiste no banco, gera OutboxMessage na mesma transação. Testes unitários e integração básica passando.

---

## Sprint 3 — API REST e Rate Limiting

**Duração:** 2–3 dias
**Objetivo:** Endpoint POST /api/v1/auctions/{id}/bids funcional via HTTP.

| Ordem | História | Descrição | Dependência |
|-------|----------|-----------|-------------|
| 1 | **HIS-08** | Queries: GetAuctionDetails, GetActiveAuctions, GetAuctionBids | Sprint 2 |
| 2 | **HIS-07** | CreateAuctionCommand + Handler | Sprint 2 |
| 3 | **HIS-13** | RedisRateLimiter com Lua script + RateLimitAttribute | Sprint 2 |
| 4 | **HIS-10** | AuctionsController com todos os endpoints | HIS-07, HIS-08, HIS-13 |
| 5 | **HIS-11** | BidsController e UsersController | HIS-10 |

**Critério de conclusão:** Curl `POST /api/v1/auctions/{id}/bids` retorna 200 com lance ou erro tipado. Swagger UI funcional. Rate limit retorna 429 após N requests.

---

## Sprint 4 — Real-Time com SignalR

**Duração:** 2–3 dias
**Objetivo:** Clientes recebem atualização em tempo real ao ocorrer um lance.

| Ordem | História | Descrição | Dependência |
|-------|----------|-----------|-------------|
| 1 | **HIS-14** | AuctionHub: JoinAuction, LeaveAuction, RequestStateSync, Redis backplane | Sprint 3 |
| 2 | **HIS-15** | Integrar Outbox → broadcast SignalR (BidPlaced, AuctionExtended, AuctionClosed) | HIS-14, HIS-05 |
| 3 | **HIS-12** | Middleware: CorrelationId, RequestLogging, SecurityHeaders | Sprint 3 |

**Critério de conclusão:** Cliente conectado ao hub recebe evento `BidPlaced` em tempo real quando outro usuário faz um lance via REST. Reconexão funcional com resync de estado.

---

## Sprint 5 — Autenticação e Autorização

**Duração:** 2 dias
**Objetivo:** Endpoints protegidos com JWT. Apenas usuários autenticados e verificados podem dar lances.

| Ordem | História | Descrição | Dependência |
|-------|----------|-----------|-------------|
| 1 | **HIS-16** | JWT authentication com Cognito (ou Keycloak/local para dev) | Sprint 4 |
| 2 | **HIS-17** | Authorization policies: CanBid, HighValueBid, Seller, Admin | HIS-16 |

**Nota:** Para desenvolvimento local, use um JWT provider local (ex: dummy auth middleware que gera tokens fixos) antes de integrar Cognito real.

**Critério de conclusão:** `POST /api/v1/auctions/{id}/bids` sem token retorna 401. Token com email não verificado retorna 403. Token válido processa o lance.

---

## Sprint 6 — Workers e Encerramento de Leilões

**Duração:** 2 dias
**Objetivo:** Leilões encerram automaticamente no tempo configurado.

| Ordem | História | Descrição | Dependência |
|-------|----------|-----------|-------------|
| 1 | **HIS-18** | AuctionCloseWorker com distributed lock Redis | Sprint 5 |
| 2 | **HIS-22** | Health Checks customizados (DB, Redis, SignalR) | Sprint 5 |

**Critério de conclusão:** Ao criar um leilão com duração de 1 minuto, após 1 minuto o status muda para `Ended`, winner é determinado, e clientes recebem `AuctionClosed` via SignalR.

---

## Sprint 7 — [MVP Completo] Qualidade e Testes

**Duração:** 2–3 dias
**Objetivo:** Sistema testável, confiável, pronto para demonstração.

| Ordem | História | Descrição | Dependência |
|-------|----------|-----------|-------------|
| 1 | **HIS-25** | Architecture Tests com NetArchTest | Sprint 6 |
| 2 | **HIS-26** | Integration Tests com Testcontainers | Sprint 6 |
| 3 | **HIS-27** | Dockerfiles e docker-compose.yml completo | Sprint 6 |

**Critério de conclusão:** `dotnet test` passa em todos os projetos. `docker compose up` inicia API + Worker + PostgreSQL + Redis.

---

## Fase 2 — Funcionalidades Avançadas (pós-MVP)

Após o MVP funcional, estas features podem ser implementadas em qualquer ordem relativa:

| Prioridade | História | Descrição |
|-----------|----------|-----------|
| 🟡 Médio | **HIS-20** | Saga AuctionClosing com compensação |
| 🟡 Médio | **HIS-21** | Event Store com DynamoDB |
| 🟡 Médio | **HIS-23** | Métricas Prometheus + Grafana |
| 🟢 Baixo | **HIS-19** | Cleanup Jobs e Snapshot Jobs |
| 🟢 Baixo | **HIS-28** | Terraform / IaC AWS |

---

## Resumo Visual por Sprint

```
Sprint 0  │ HIS-29  HIS-03
          │ (correções + packages)
──────────┤
Sprint 1  │ HIS-01  HIS-02  HIS-24
          │ (Shared + Domain + Unit Tests)
──────────┤
Sprint 2  │ HIS-04  HIS-05  HIS-06  HIS-09
          │ (EF Core + Outbox + PlaceBid + Behaviors)
──────────┤
Sprint 3  │ HIS-07  HIS-08  HIS-13  HIS-10  HIS-11
          │ (Commands/Queries + Rate Limit + Controllers)
──────────┤
Sprint 4  │ HIS-14  HIS-15  HIS-12
          │ (SignalR Hubs + Broadcast + Middleware)
──────────┤
Sprint 5  │ HIS-16  HIS-17
          │ (JWT Auth + Policies)
──────────┤
Sprint 6  │ HIS-18  HIS-22
          │ (Workers + Health Checks)
──────────┤
Sprint 7  │ HIS-25  HIS-26  HIS-27
          │ (Arch Tests + Integration Tests + Docker)
──────────┤
[MVP ✅]
──────────┤
Fase 2    │ HIS-20  HIS-21  HIS-23  HIS-19  HIS-28
          │ (Saga + DynamoDB + Prometheus + Jobs + IaC)
```

---

## Pontos de Atenção

1. **Cognito em dev:** Configure um mock de autenticação JWT local para não bloquear desenvolvimento nos Sprints 1-5. Integrar Cognito real apenas no Sprint 5.

2. **DynamoDB Event Store:** É opcional para o MVP. Use apenas EF Core/PostgreSQL para persistência até que Event Sourcing seja necessário (Fase 2).

3. **Teste de carga:** O projeto `AuctionKings.LoadTests` com k6 deve ser executado apenas após o MVP completo, em ambiente isolado.

4. **AWS Services:** SQS, SNS, Secrets Manager são opcionais para desenvolvimento local. Use Redis + fila em memória como fallback.

5. **Anti-sniping:** Está dentro da lógica de domínio do `Auction.PlaceBid()` — já coberto pela HIS-02.
