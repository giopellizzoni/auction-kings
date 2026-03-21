# Task 14 — Implementar Unit Tests Completos

## Contexto

O projeto `AuctionKings.UnitTests` contém apenas um teste vazio placeholder. Unit tests devem cobrir a lógica de domínio (aggregates, value objects, domain services), command/query handlers (com mocks de repositórios), pipeline behaviors e validators. São os testes mais rápidos e devem formar a base da pirâmide de testes.

## Arquivos a Alterar/Criar

- `test/AuctionKings.UnitTests/AuctionKings.UnitTests.csproj` — adicionar project refs para Domain, Application, Shared; adicionar `Moq` ou `NSubstitute`, `FluentAssertions`
- `Directory.Packages.props` — adicionar versões de `NSubstitute`, `FluentAssertions`
- `test/AuctionKings.UnitTests/Domain/AuctionTests.cs` — testes do aggregate Auction (criar, PlaceBid válido/inválido, Close, Extend)
- `test/AuctionKings.UnitTests/Domain/BidTests.cs` — testes da entity Bid
- `test/AuctionKings.UnitTests/Domain/ValueObjects/MoneyTests.cs` — testes de Money (aritmética, validação, igualdade)
- `test/AuctionKings.UnitTests/Domain/ValueObjects/BidIncrementTests.cs` — testes de incremento por faixa
- `test/AuctionKings.UnitTests/Application/Commands/CreateAuctionCommandHandlerTests.cs`
- `test/AuctionKings.UnitTests/Application/Commands/PlaceBidCommandHandlerTests.cs`
- `test/AuctionKings.UnitTests/Application/Commands/CloseAuctionCommandHandlerTests.cs`
- `test/AuctionKings.UnitTests/Application/Queries/GetAuctionDetailsQueryHandlerTests.cs`
- `test/AuctionKings.UnitTests/Application/Queries/GetActiveAuctionsQueryHandlerTests.cs`
- `test/AuctionKings.UnitTests/Application/Behaviors/ValidationBehaviorTests.cs`
- `test/AuctionKings.UnitTests/Application/Behaviors/LoggingBehaviorTests.cs`
- `test/AuctionKings.UnitTests/Shared/ResultTests.cs` — testes de Result<T> e Error
- `test/AuctionKings.UnitTests/Shared/EntityTests.cs` — testes de igualdade de Entity
- Remover `test/AuctionKings.UnitTests/UnitTest1.cs`

## Critérios de Aceite

- Cobertura mínima de 80% nas camadas Domain e Application
- Todos os testes passam com `dotnet test test/AuctionKings.UnitTests/`
- Testes de domain são puramente in-memory (zero dependência de infra)
- Testes de handlers usam mocks/substitutes para repositórios e serviços
- Naming convention: `MethodUnderTest_Scenario_ExpectedBehavior`
- Cada teste verifica uma única coisa (single assertion ou assertions relacionadas)
- Testes rodam em < 5 segundos total

## Testes Obrigatórios

Todos os testes listados nas Tasks 01-05 e 07 devem estar aqui, totalizando no mínimo:
- 10+ testes de Domain (Auction, Bid, Value Objects)
- 7+ testes de Command Handlers
- 5+ testes de Query Handlers
- 4+ testes de Pipeline Behaviors
- 5+ testes de Shared (Result, Entity, AggregateRoot)

## Riscos/Observações

- Escolher entre `Moq` e `NSubstitute` — NSubstitute é mais legível e não tem issues de licença recentes
- `FluentAssertions` melhora legibilidade mas adiciona dependência — avaliar se vale vs assertions nativas do xUnit
- Não testar coisas triviais (getters/setters simples) — focar em lógica de negócio
- Testes de handlers devem verificar side effects (calls ao repositório, events emitidos)

## Estimativa

**L** (Large)

## Dependências

- **Task 01** — classes base testáveis
- **Task 02** — domain model implementado
- **Task 03** — pipeline behaviors implementados
- **Task 04** — command handlers implementados
- **Task 05** — query handlers implementados

## Status

🔄 **In Progress** (domain unit tests done via issue #14; application/behavior tests pending)

## GitHub Issues

| # | Title | Status |
|---|-------|--------|
| [#14](https://github.com/PellizzoniCode/auction-kings/issues/14) | Unit tests do Domínio (Auction/Bid/VOs) | ✅ closed |
| [#59](https://github.com/PellizzoniCode/auction-kings/issues/59) | [EPIC] W7 Testes Sênior (Unit/Integration/Architecture) + Load Test | 🔴 open |
