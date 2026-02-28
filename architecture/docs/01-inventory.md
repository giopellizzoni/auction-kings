# Inventário do Repositório — AuctionKings

> Gerado em: 2026-02-28
> Fonte de verdade da arquitetura: `/architecture/ARCHITECTURE.md`

---

## 1. Projetos (.csproj)

| Projeto | Caminho | Tipo | Status |
|---------|---------|------|--------|
| AuctionKings.API | `src/services/AuctionKings.API/` | ASP.NET Core Web API | Scaffold mínimo |
| AuctionKings.Application | `src/services/AuctionKings.Application/` | Class Library | Placeholder (Class1.cs) |
| AuctionKings.Domain | `src/services/AuctionKings.Domain/` | Class Library | Placeholder (Class1.cs) |
| AuctionKings.Infrastructure | `src/services/AuctionKings.Infrastructure/` | Class Library | Placeholder (Class1.cs) |
| AuctionKings.Shared | `src/common/AuctionKings.Shared/` | Class Library | Stubs vazios |
| AuctionKings.BackgroundWorkers | `src/workers/AuctionKings.BackgroundWorkers/` | Console App | "Hello World" placeholder |
| AuctionKings.UnitTests | `test/AuctionKings.UnitTests/` | xUnit | Stub vazio |
| AuctionKings.IntegrationTests | `test/AuctionKings.IntegrationTests/` | xUnit | Stub vazio |
| AuctionKings.ArchitectureTests | `test/AuctionKings.ArchitectureTests/` | xUnit | Stub vazio |
| AuctionKings.LoadTests | `test/AuctionKings.LoadTests/` | xUnit | Stub vazio |

> **Observação:** existe um typo no nome do arquivo .csproj da Application: `AuctionKings.Applciation.csproj` (falta o 'i').

---

## 2. Árvore de Diretórios

```
auction-kings/
├── src/
│   ├── common/
│   │   └── AuctionKings.Shared/
│   │       └── Common/
│   │           ├── Entity.cs               — STUB (classe vazia)
│   │           └── AggregateRoot.cs        — STUB (classe vazia)
│   ├── services/
│   │   ├── AuctionKings.API/
│   │   │   ├── Controllers/
│   │   │   │   ├── AuctionsController.cs   — STUB (sem endpoints)
│   │   │   │   ├── BidsController.cs       — STUB (sem endpoints)
│   │   │   │   └── UsersControllers.cs     — STUB (sem endpoints)
│   │   │   ├── Filters/
│   │   │   │   ├── GlobalExceptionFilter.cs — STUB (corpo vazio)
│   │   │   │   ├── IdempotencyFilter.cs    — STUB (corpo vazio)
│   │   │   │   └── RateLimitAttribute.cs   — COMENTADO (toda a classe)
│   │   │   ├── Hubs/
│   │   │   │   ├── AuctionHub.cs           — STUB (sem métodos)
│   │   │   │   └── NotificationHub.cs      — STUB (sem métodos)
│   │   │   └── Program.cs                  — IMPLEMENTAÇÃO MÍNIMA
│   │   ├── AuctionKings.Application/
│   │   │   └── Class1.cs                   — PLACEHOLDER
│   │   ├── AuctionKings.Domain/
│   │   │   ├── Aggregates/                 — PASTA VAZIA
│   │   │   └── Class1.cs                   — PLACEHOLDER
│   │   └── AuctionKings.Infrastructure/
│   │       └── Class1.cs                   — PLACEHOLDER
│   └── workers/
│       └── AuctionKings.BackgroundWorkers/
│           └── Program.cs                  — "Hello, World!" placeholder
├── test/
│   ├── AuctionKings.UnitTests/
│   │   └── UnitTest1.cs                    — STUB (teste vazio)
│   ├── AuctionKings.IntegrationTests/
│   │   └── UnitTest1.cs                    — STUB
│   ├── AuctionKings.ArchitectureTests/
│   │   └── UnitTest1.cs                    — STUB
│   └── AuctionKings.LoadTests/
│       └── UnitTest1.cs                    — STUB
├── docker/
│   ├── docker-compose.infra.yml            — COMPLETO (PostgreSQL + Redis)
│   ├── docker-compose.yml                  — VAZIO (1 linha)
│   ├── .env                                — CONFIGURADO
│   └── secrets/                            — arquivos de senha (não commitados)
├── architecture/
│   └── ARCHITECTURE.md                     — ESPECIFICAÇÃO COMPLETA v2.0
├── docs/                                   — VAZIO
├── scripts/                                — VAZIO
└── iac/                                    — VAZIO
```

---

## 3. Classes Implementadas — Detalhe

### API Layer (`AuctionKings.API`)

#### Controllers

| Arquivo | Classe | Atributos de Rota | Endpoints | Status |
|---------|--------|-------------------|-----------|--------|
| `Controllers/AuctionsController.cs` | `AuctionsController : ControllerBase` | `[Route("api/[controller]")]`, `[ApiController]` | **Nenhum** | Stub |
| `Controllers/BidsController.cs` | `BidsController : ControllerBase` | `[Route("api/[controller]")]`, `[ApiController]` | **Nenhum** | Stub |
| `Controllers/UsersControllers.cs` | `UsersControllers : ControllerBase` | `[Route("api/[controller]")]`, `[ApiController]` | **Nenhum** | Stub |

#### Hubs (SignalR)

| Arquivo | Classe | Métodos de Hub | Status |
|---------|--------|----------------|--------|
| `Hubs/AuctionHub.cs` | `AuctionHub` | **Nenhum** | Stub |
| `Hubs/NotificationHub.cs` | `NotificationHub` | **Nenhum** | Stub |

#### Filters

| Arquivo | Classe | Status |
|---------|--------|--------|
| `Filters/GlobalExceptionFilter.cs` | `GlobalExceptionFilter` | Stub (corpo vazio) |
| `Filters/IdempotencyFilter.cs` | `IdempotencyFilter` | Stub (corpo vazio) |
| `Filters/RateLimitAttribute.cs` | `RateLimitAttribute` | Comentado |

#### Program.cs — Configurações Ativas

```csharp
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live");
```

**Ausente no Program.cs:** SignalR, MediatR, EF Core, Redis, autenticação JWT, middlewares customizados.

---

### Shared Layer (`AuctionKings.Shared`)

| Arquivo | Classe | Conteúdo Atual | Conteúdo Esperado |
|---------|--------|----------------|-------------------|
| `Common/Entity.cs` | `Entity` | Classe vazia | Id, igualdade por valor, GetHashCode |
| `Common/AggregateRoot.cs` | `AggregateRoot` | Classe vazia | DomainEvents list, AddDomainEvent, ClearDomainEvents |

---

### Application Layer (`AuctionKings.Application`)

| Tipo | Arquivo | Status |
|------|---------|--------|
| Commands | — | **Não existe** |
| Queries | — | **Não existe** |
| Handlers | — | **Não existe** |
| Behaviors (Pipeline) | — | **Não existe** |
| DTOs | — | **Não existe** |
| Validators | — | **Não existe** |
| Sagas | — | **Não existe** |

---

### Domain Layer (`AuctionKings.Domain`)

| Tipo | Arquivo | Status |
|------|---------|--------|
| Aggregates | — | **Não existe** |
| Entities | — | **Não existe** |
| Value Objects | — | **Não existe** |
| Domain Events | — | **Não existe** |
| Domain Errors | — | **Não existe** |
| Enums | — | **Não existe** |
| Specifications | — | **Não existe** |

---

### Infrastructure Layer (`AuctionKings.Infrastructure`)

| Tipo | Arquivo | Status |
|------|---------|--------|
| DbContext (EF Core) | — | **Não existe** |
| Repositories | — | **Não existe** |
| UnitOfWork | — | **Não existe** |
| Outbox Messages | — | **Não existe** |
| Event Store (DynamoDB) | — | **Não existe** |
| Redis Cache | — | **Não existe** |
| SignalR Notification Service | — | **Não existe** |
| Background Services | — | **Não existe** |
| Authorization Handlers | — | **Não existe** |
| Rate Limiter | — | **Não existe** |
| Health Checks | — | **Não existe** |
| Migrations | — | **Não existe** |

---

### Background Workers (`AuctionKings.BackgroundWorkers`)

| Worker | Status |
|--------|--------|
| OutboxProcessor | **Não existe** |
| AuctionCloseWorker | **Não existe** |
| BidProcessorWorker | **Não existe** |
| NotificationWorker | **Não existe** |
| ProjectionWorker | **Não existe** |
| DlqProcessorWorker | **Não existe** |

---

## 4. NuGet Packages Configurados

### `Directory.Packages.props` (configurados)

| Package | Versão | Finalidade |
|---------|--------|------------|
| Microsoft.AspNetCore.OpenApi | 10.0.0 | OpenAPI/Swagger |
| Microsoft.VisualStudio.Web.CodeGeneration.Design | 10.0.2 | Scaffolding |
| coverlet.collector | 6.0.4 | Cobertura de testes |
| Microsoft.NET.Test.Sdk | 17.14.1 | SDK de testes |
| xunit | 2.9.3 | Framework de testes |
| xunit.runner.visualstudio | 3.1.4 | Runner visual |

### Packages **ausentes** (necessários pela arquitetura)

| Package | Finalidade |
|---------|------------|
| Microsoft.AspNetCore.SignalR | SignalR |
| Microsoft.AspNetCore.SignalR.StackExchangeRedis | Backplane Redis |
| MessagePack.AspNetCoreMvcFormatter | Protocolo binário SignalR |
| MediatR | CQRS / Mediator |
| MediatR.Extensions.Microsoft.DependencyInjection | DI MediatR |
| Microsoft.EntityFrameworkCore | ORM |
| Npgsql.EntityFrameworkCore.PostgreSQL | Driver PostgreSQL |
| Microsoft.EntityFrameworkCore.Design | Migrations |
| StackExchange.Redis | Cliente Redis |
| Microsoft.Extensions.Caching.StackExchangeRedis | Distributed cache |
| FluentValidation.AspNetCore | Validação |
| AWSSDK.DynamoDBv2 | Event Store DynamoDB |
| AWSSDK.SimpleSystemsManagement | Secrets Manager |
| Amazon.Extensions.NETCore.Setup | SDK AWS |
| Microsoft.AspNetCore.Authentication.JwtBearer | Autenticação JWT |
| Prometheus-net.AspNetCore | Métricas |
| NetArchTest.Rules | Testes de arquitetura |

---

## 5. Infraestrutura Docker

### `docker-compose.infra.yml` — Status: PRONTO

| Serviço | Imagem | Porta | Health Check | Volume |
|---------|--------|-------|--------------|--------|
| PostgreSQL 16 | postgres:16-alpine | 5432 | pg_isready (20 retries) | pg_data |
| Redis 7 | redis:7-alpine | 6379 | redis-cli ping (20 retries) | redis_data |

### Configuração via `.env`

```
POSTGRES_USER=dev
POSTGRES_DB=AuctionKingsDB
POSTGRES_HOST_PORT=5432
REDIS_HOST_PORT=6379
```

---

## 6. Endpoints HTTP Existentes

| Verbo | Rota | Controlador | Status |
|-------|------|-------------|--------|
| GET | `/health/live` | HealthChecks | **FUNCIONANDO** |
| GET | `/openapi/v1.json` | OpenAPI (dev only) | **FUNCIONANDO** |
| * | `/api/auctions/*` | AuctionsController | **NÃO IMPLEMENTADO** |
| * | `/api/bids/*` | BidsController | **NÃO IMPLEMENTADO** |
| * | `/api/users/*` | UsersControllers | **NÃO IMPLEMENTADO** |

---

## 7. CI/CD

| Arquivo | Caminho | Status |
|---------|---------|--------|
| GitHub Actions workflow | `.github/workflows/` | Configurado (citado em commits) |

---

## Resumo de Status por Camada

| Camada | Estrutura | Implementação | % Completo |
|--------|-----------|---------------|------------|
| Domain | Pasta criada | Vazio | ~0% |
| Application | Pasta criada | Vazio | ~0% |
| Infrastructure | Pasta criada | Vazio | ~0% |
| API Controllers | Stubs criados | Sem endpoints | ~5% |
| API Hubs | Stubs criados | Sem métodos | ~5% |
| API Filters | Stubs criados | Sem lógica | ~5% |
| Shared Base | Stubs criados | Classes vazias | ~5% |
| Background Workers | Placeholder | Vazio | ~2% |
| Tests | Stubs criados | Sem asserções | ~2% |
| Docker Infra | COMPLETO | PostgreSQL + Redis | ~90% |
| Packages/Build | Parcial | Faltam deps críticas | ~20% |
