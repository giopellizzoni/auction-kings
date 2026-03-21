# Task 18 — Implementar Load Tests com NBomber

## Contexto

O projeto `AuctionKings.LoadTests` existe com teste placeholder. Load tests validam que o sistema suporta a carga esperada: múltiplos bids simultâneos, listagens concorrentes, conexões SignalR em massa. NBomber é o framework recomendado para .NET — permite definir cenários declarativamente e gera relatórios HTML.

## Arquivos a Alterar/Criar

- `test/AuctionKings.LoadTests/AuctionKings.LoadTests.csproj` — adicionar `NBomber`, `NBomber.Http`, project ref para API
- `Directory.Packages.props` — adicionar versões de `NBomber`, `NBomber.Http`
- `test/AuctionKings.LoadTests/Scenarios/PlaceBidLoadTest.cs` — cenário: N usuários fazendo bids simultâneos no mesmo leilão
- `test/AuctionKings.LoadTests/Scenarios/ListAuctionsLoadTest.cs` — cenário: N requests simultâneos ao endpoint de listagem
- `test/AuctionKings.LoadTests/Scenarios/SignalRLoadTest.cs` — cenário: N conexões SignalR simultâneas recebendo updates
- `test/AuctionKings.LoadTests/LoadTestBase.cs` — base class com setup de `WebApplicationFactory` ou URL de target
- Remover `test/AuctionKings.LoadTests/UnitTest1.cs`

## Critérios de Aceite

- `PlaceBidLoadTest` simula 100 usuários fazendo bids em 10 leilões durante 30 segundos
- `ListAuctionsLoadTest` simula 200 requests/segundo ao GET /api/auctions por 30 segundos
- Cenários reportam: requests/sec, latência p50/p95/p99, error rate
- Todos os cenários passam com < 1% error rate e p99 < 500ms (em ambiente local)
- Relatório HTML gerado automaticamente ao final
- Load tests NÃO rodam no CI por padrão (apenas manualmente)

## Testes Obrigatórios

- `PlaceBidLoadTest_ShouldHandleConcurrentBids`
- `ListAuctionsLoadTest_ShouldHandleHighThroughput`
- `SignalRLoadTest_ShouldHandleManyConnections`

## Riscos/Observações

- Load tests devem rodar contra a API completa (não mocks) — precisa de DB e Redis rodando
- Resultados variam muito por máquina — usar como baseline, não como gate no CI
- NBomber gera relatórios em `./reports/` — adicionar ao `.gitignore`
- Considerar k6 como alternativa se a equipe preferir scripts JavaScript para load tests
- SignalR load testing é complexo — pode precisar de `Microsoft.AspNetCore.SignalR.Client`

## Estimativa

**M** (Medium)

## Dependências

- **Task 10** — controllers implementados (endpoints para testar)
- **Task 09** — SignalR hubs implementados (para teste de conexões)
- **Task 06** — infraestrutura completa (DB real necessário)
- **Task 08** — Redis configurado

## Status

⏳ **Not Started**

## GitHub Issues

| # | Title | Status |
|---|-------|--------|
| [#59](https://github.com/PellizzoniCode/auction-kings/issues/59) | [EPIC] W7 Testes Sênior (Unit/Integration/Architecture) + Load Test | 🔴 open |
| [#62](https://github.com/PellizzoniCode/auction-kings/issues/62) | Criar k6 load test para PlaceBid | 🔴 open |
| [#63](https://github.com/PellizzoniCode/auction-kings/issues/63) | Executar tuning básico (índices, queries) baseado no k6 | 🔴 open |
| [#64](https://github.com/PellizzoniCode/auction-kings/issues/64) | Documentar resultados do load test (p95, throughput) | 🔴 open |
