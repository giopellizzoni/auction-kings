# Task 06 — Implementar Infrastructure: EF Core DbContext, Configurations, Repositories e UnitOfWork

## Contexto

A camada de infraestrutura implementa as interfaces definidas na Application Layer usando EF Core com PostgreSQL. O `AuctionDbContext` é o ponto central de acesso a dados, com configurations que definem o schema (conforme spec: tabelas `auctions`, `bids`, `outbox_messages`). O `UnitOfWork` envelopa o `SaveChangesAsync` do EF Core com coleta de domain events para o Outbox Pattern.

## Arquivos a Alterar/Criar

- `src/services/AuctionKings.Infrastructure/AuctionKings.Infrastructure.csproj` — adicionar: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, project refs para Domain, Application, Shared
- `Directory.Packages.props` — adicionar versões dos pacotes EF Core e Npgsql
- `src/services/AuctionKings.Infrastructure/Persistence/AuctionDbContext.cs` — DbContext com `DbSet<Auction>`, `DbSet<Bid>`, `DbSet<OutboxMessage>`, override `OnModelCreating`
- `src/services/AuctionKings.Infrastructure/Persistence/Configurations/AuctionConfiguration.cs` — `IEntityTypeConfiguration<Auction>`: table name, column mappings, `xmin` concurrency token, indexes
- `src/services/AuctionKings.Infrastructure/Persistence/Configurations/BidConfiguration.cs` — FK para Auction, indexes em `AuctionId` + `Amount`
- `src/services/AuctionKings.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs` — tabela `outbox_messages` com `Type`, `Content` (jsonb), `ProcessedAt`, index em `ProcessedAt IS NULL`
- `src/services/AuctionKings.Infrastructure/Persistence/OutboxMessage.cs` — entity para o outbox
- `src/services/AuctionKings.Infrastructure/Repositories/AuctionRepository.cs` — implementa `IAuctionRepository` usando DbContext
- `src/services/AuctionKings.Infrastructure/Repositories/BidRepository.cs` — implementa `IBidRepository`
- `src/services/AuctionKings.Infrastructure/Persistence/UnitOfWork.cs` — implementa `IUnitOfWork`, coleta domain events dos aggregates, serializa como `OutboxMessage`, commit em transação única
- `src/services/AuctionKings.Infrastructure/DependencyInjection.cs` — extension method `AddInfrastructure(this IServiceCollection, IConfiguration)` registrando DbContext, repositories, UoW
- Remover `src/services/AuctionKings.Infrastructure/Class1.cs`
- Migration inicial via `dotnet ef migrations add InitialCreate`

## Critérios de Aceite

- `AuctionDbContext` mapeia corretamente para PostgreSQL com snake_case naming convention
- Concorrência otimista via `xmin` system column do PostgreSQL no Auction
- `UnitOfWork.CommitAsync` coleta domain events de todos aggregates tracked, cria `OutboxMessage` para cada, salva tudo numa transação
- Repositories são thin wrappers sobre DbContext — sem lógica de negócio
- Migration gera schema compatível com a spec (tabelas `auctions`, `bids`, `outbox_messages`)
- Connection string lida de configuration com suporte a Docker secrets

## Testes Obrigatórios

- `AuctionRepository_AddAndRetrieve_ShouldWork` (integration com Testcontainers PostgreSQL)
- `AuctionRepository_Update_WithStaleVersion_ShouldThrowConcurrencyException`
- `BidRepository_GetByAuctionId_ShouldReturnOrderedBids`
- `UnitOfWork_Commit_ShouldPersistOutboxMessages`
- `UnitOfWork_Commit_ShouldClearDomainEvents`
- `AuctionConfiguration_ShouldMapColumnsCorrectly` (unit test com in-memory ou snapshot)

## Riscos/Observações

- `xmin` é específico do PostgreSQL — considerar abstração se migração para outro DB for possibilidade futura
- Migrations devem ser idempotentes para CI/CD
- O `OutboxMessage.Content` usa `jsonb` do PostgreSQL para performance em queries
- Incluir `EFCore.NamingConventions` para snake_case automático ou mapear manualmente

## Estimativa

**L** (Large)

## Dependências

- **Task 01** — `Entity`, `AggregateRoot` (para mapeamento EF Core)
- **Task 02** — `Auction`, `Bid`, domain events (entidades a persistir)
- **Task 03** — interfaces `IAuctionRepository`, `IBidRepository`, `IUnitOfWork` (a implementar)

## Status

✅ **Completed** — DbContext, EF Core configurations (AuctionConfiguration, BidConfiguration, OutboxMessageConfiguration, IdempotencyKeyConfiguration), AuctionRepository, BidRepository, UnitOfWork (with outbox integration), xmin concurrency token (shadow property), and InitialCreate migration all implemented.

## GitHub Issues

| # | Title | Status |
|---|-------|--------|
| [#15](https://github.com/PellizzoniCode/auction-kings/issues/15) | [EPIC] W2 Infra de Dados + API MVP | ✅ closed |
| [#16](https://github.com/PellizzoniCode/auction-kings/issues/16) | Configurar EF Core + Postgres (DbContext) | ✅ closed |
| [#17](https://github.com/PellizzoniCode/auction-kings/issues/17) | Definir schema inicial (Auctions, Bids) | ✅ closed |
| [#18](https://github.com/PellizzoniCode/auction-kings/issues/18) | Implementar Repositories básicos | ✅ closed |
| [#31](https://github.com/PellizzoniCode/auction-kings/issues/31) | Configurar concorrência otimista via xmin (Postgres) | ✅ closed |
| [#35](https://github.com/PellizzoniCode/auction-kings/issues/35) | Criar Outbox table e modelo | ✅ closed |
