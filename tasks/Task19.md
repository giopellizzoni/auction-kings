# Task 19 — Finalização do Projeto: Security, Observability, DevOps e Docs

## Contexto

Todas as features de negócio estão implementadas (T01–T18). Esta task agrupa o trabalho restante para deixar o projeto pronto para demonstração e produção: autenticação, observabilidade, containerização e documentação final. As subtasks estão ordenadas por prioridade (P0 → P1 → P2).

---

## Subtasks

### 🔐 Security

#### ST01 — Implementar autenticação JWT (local) [`#49`](https://github.com/PellizzoniCode/auction-kings/issues/49) · P0

Gerar e validar JWTs localmente (sem Cognito por enquanto) e proteger os endpoints de bid.

**Aceite:**

- `POST /api/v1/auctions/{id}/bids` requer header `Authorization: Bearer <token>`
- Token inválido/ausente retorna 401
- Geração de token via endpoint de dev (ex: `POST /dev/token`) ou script

**Arquivos:**

- `src/services/AuctionKings.API/Auth/JwtTokenGenerator.cs` — gerador de token para dev
- `src/services/AuctionKings.API/Program.cs` — `AddAuthentication().AddJwtBearer(...)`
- `src/services/AuctionKings.API/Controllers/BidsController.cs` — `[Authorize]`
- `appsettings.json` / `appsettings.Development.json` — `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`

---

#### ST02 — Criar Authorization Policies (Bidder/Admin) [`#50`](https://github.com/PellizzoniCode/auction-kings/issues/50) · P1

Definir policies `Bidder` e `Admin` baseadas em claims do JWT e aplicar nos endpoints relevantes.

**Aceite:**

- Policy `Bidder` protege PlaceBid
- Policy `Admin` protege CloseAuction
- Policies configuradas via `AddAuthorization(options => ...)`

**Arquivos:**

- `src/services/AuctionKings.API/Auth/Policies.cs` — constantes de policy names
- `src/services/AuctionKings.API/Program.cs` — registro das policies
- Controllers relevantes — `[Authorize(Policy = "Admin")]` etc.

**Dependência:** ST01

---

#### ST03 — [Stretch] Preparar integração AWS Cognito (documento + POC) [`#52`](https://github.com/PellizzoniCode/auction-kings/issues/52) · P2

Documento com passos para substituir JWT local por Cognito + POC mínimo.

**Aceite:**

- `docs/cognito-integration.md` com passos e diagrama
- POC não precisa virar produção agora

**Dependência:** ST01, ST02

---

### 📊 Observability

#### ST04 — Adicionar logging estruturado (Serilog) [`#54`](https://github.com/PellizzoniCode/auction-kings/issues/54) · P0

Substituir o logger padrão do ASP.NET Core por Serilog com output JSON, enriquecido com `RequestId` e `TraceId`.

**Aceite:**

- Logs em JSON no console (produção-ready)
- `RequestId` e `CorrelationId` presentes em todos os logs do request pipeline
- Nível configurável via `appsettings.json`

**Arquivos:**

- `Directory.Packages.props` — `Serilog.AspNetCore`, `Serilog.Sinks.Console`
- `src/services/AuctionKings.API/Program.cs` — `UseSerilog(...)`
- `src/services/AuctionKings.API/appsettings.json` — configuração de Serilog

---

#### ST05 — Adicionar métricas Prometheus básicas [`#57`](https://github.com/PellizzoniCode/auction-kings/issues/57) · P1

Expor endpoint `/metrics` com métricas de request duration, bids placed e outbox lag.

**Aceite:**

- `GET /metrics` retorna formato Prometheus
- Métricas: request duration (histogram), bids placed (counter), outbox pending messages (gauge)

**Arquivos:**

- `Directory.Packages.props` — `prometheus-net.AspNetCore`
- `src/services/AuctionKings.API/Program.cs` — `UseMetricServer()`, `UseHttpMetrics()`
- `src/services/AuctionKings.Infrastructure/Outbox/OutboxMetrics.cs` — gauge do outbox lag

---

#### ST06 — [Stretch] Dashboard Grafana (dev) [`#58`](https://github.com/PellizzoniCode/auction-kings/issues/58) · P2

Subir Grafana no docker compose com datasource Prometheus e dashboard básico.

**Aceite:**

- `docker compose up` inclui Grafana na porta 3000
- Dashboard com métricas principais visíveis

**Arquivos:**

- `docker/docker-compose.infra.yml` — serviço Grafana + Prometheus
- `docker/grafana/provisioning/` — datasource e dashboard JSON

**Dependência:** ST05

---

### 🐳 DevOps / Docker

#### ST07 — Dockerfile da API + docker-compose final (app + deps) [`#67`](https://github.com/PellizzoniCode/auction-kings/issues/67) · P0

Criar Dockerfile multi-stage para a API e adicionar o serviço `api` ao docker-compose junto às dependências existentes.

**Aceite:**

- `docker compose up` sobe PostgreSQL, Redis, LocalStack + API
- Dockerfile usa multi-stage (build → runtime) com imagem base `mcr.microsoft.com/dotnet/aspnet:10.0`
- API saudável em `http://localhost:5081/health/live` após `docker compose up`

**Arquivos:**

- `src/services/AuctionKings.API/Dockerfile`
- `.dockerignore`
- `docker/docker-compose.yml` — compose completo (infra + app)

---

#### ST08 — Pipeline de build de imagem Docker (GHCR) [`#68`](https://github.com/PellizzoniCode/auction-kings/issues/68) · P1

Adicionar job no CI para buildar e publicar a imagem Docker no GitHub Container Registry por tag/sha.

**Aceite:**

- Imagem publicada em `ghcr.io/pellizzonicode/auction-kings:sha-<commit>`
- Job separado do build-and-test, acionado em push para `main`

**Arquivos:**

- `.github/workflows/docker.yml` — novo workflow de build + push

**Dependência:** ST07

---

#### ST09 — [Stretch] Deploy staging AWS (ECS + RDS + Redis + SQS) [`#69`](https://github.com/PellizzoniCode/auction-kings/issues/69) · P2

Infraestrutura mínima no AWS para staging: ECS Fargate, RDS PostgreSQL, ElastiCache Redis, SQS.

**Aceite:**

- Endpoint público de staging funcionando
- Secrets via AWS Secrets Manager
- Logs no CloudWatch

**Dependência:** ST07, ST08

---

### 📄 Documentação

#### ST10 — Documentar trade-offs (Concorrência / Idempotência / Outbox) [`#41`](https://github.com/PellizzoniCode/auction-kings/issues/41) · P1

Criar `docs/decisions.md` com ADR curto explicando as escolhas de design.

**Aceite:**

- Explica por que `xmin` para concorrência otimista
- Explica por que Outbox Pattern
- Documenta limites atuais do sistema

**Arquivos:**

- `docs/decisions.md`

---

#### ST11 — Escrever docs/architecture.md final + ADRs [`#70`](https://github.com/PellizzoniCode/auction-kings/issues/70) · P0

Documento de arquitetura consolidado: visão geral, trade-offs, decisões, pronto para entrevista técnica.

**Aceite:**

- Cobre: Clean Architecture, DDD, CQRS, Outbox, SignalR, concorrência, AWS roadmap
- Inclui diagrama (Mermaid ou ASCII)
- ADRs referenciados

**Arquivos:**

- `docs/architecture.md` — documento principal (atualização de `/architecture/ARCHITECTURE.md`)

**Dependência:** ST10

---

#### ST12 — Runbook básico (como operar + troubleshooting) [`#71`](https://github.com/PellizzoniCode/auction-kings/issues/71) · P1

Guia operacional documentando health checks, logs, outbox lag e falhas comuns.

**Aceite:**

- Cobre: como checar `/health/ready`, como ler logs, como monitorar outbox
- Seção de troubleshooting com cenários comuns (DB down, Redis down, outbox stuck)

**Arquivos:**

- `docs/runbook.md`

---

#### ST13 — Checklist final: Demo script de 5 minutos [`#72`](https://github.com/PellizzoniCode/auction-kings/issues/72) · P0

Roteiro repetível de demonstração do sistema end-to-end.

**Aceite:**

- Criar leilão → 3 clientes conectados via SignalR → 20 lances concorrentes → mostrar logs/métricas
- Demo repetível (script ou curl commands)

**Arquivos:**

- `docs/demo-script.md`

---

## Ordem de Execução Recomendada

```
P0 (bloqueantes ou de maior impacto):
  ST04 (Serilog) → ST01 (JWT) → ST07 (Dockerfile) → ST11 (architecture.md) → ST13 (demo script)

P1 (importante antes de entrevista):
  ST02 (Auth Policies) → ST05 (Prometheus) → ST08 (GHCR pipeline) → ST10 (trade-offs) → ST12 (runbook)

P2 (stretch goals):
  ST03 (Cognito POC) → ST06 (Grafana) → ST09 (AWS staging)
```

## Estimativa

**L** (Large — múltiplas subtasks independentes)

## Status

🔄 **In Progress** (P0 complete)

## GitHub Issues

| #                                                                | Title                                                           | Priority | Status  |
| ---------------------------------------------------------------- | --------------------------------------------------------------- | -------- | ------- |
| [#49](https://github.com/PellizzoniCode/auction-kings/issues/49) | Implementar autenticação JWT (local)                            | P0       | ✅ closed |
| [#54](https://github.com/PellizzoniCode/auction-kings/issues/54) | Adicionar logging estruturado (Serilog)                         | P0       | ✅ closed |
| [#67](https://github.com/PellizzoniCode/auction-kings/issues/67) | Adicionar Dockerfile da API + docker compose final (app + deps) | P0       | ✅ closed |
| [#70](https://github.com/PellizzoniCode/auction-kings/issues/70) | Escrever docs/architecture.md final + ADRs                      | P0       | ✅ closed |
| [#72](https://github.com/PellizzoniCode/auction-kings/issues/72) | Checklist final: Demo script de 5 minutos                       | P0       | ✅ closed |
| [#41](https://github.com/PellizzoniCode/auction-kings/issues/41) | Documentar trade-offs (Concorrência/Idempotência/Outbox)        | P1       | 🔴 open |
| [#50](https://github.com/PellizzoniCode/auction-kings/issues/50) | Criar Authorization Policies (Bidder/Admin)                     | P1       | 🔴 open |
| [#57](https://github.com/PellizzoniCode/auction-kings/issues/57) | Adicionar métricas Prometheus básicas                           | P1       | 🔴 open |
| [#68](https://github.com/PellizzoniCode/auction-kings/issues/68) | Adicionar pipeline para build de imagem (GHCR)                  | P1       | 🔴 open |
| [#71](https://github.com/PellizzoniCode/auction-kings/issues/71) | Runbook básico (como operar + troubleshooting)                  | P1       | 🔴 open |
| [#52](https://github.com/PellizzoniCode/auction-kings/issues/52) | [Stretch] Preparar integração AWS Cognito (documento + POC)     | P2       | 🔴 open |
| [#58](https://github.com/PellizzoniCode/auction-kings/issues/58) | [Stretch] Dashboard Grafana (dev)                               | P2       | 🔴 open |
| [#69](https://github.com/PellizzoniCode/auction-kings/issues/69) | [Stretch] Deploy staging AWS (ECS + RDS + Redis + SQS)          | P2       | 🔴 open |
