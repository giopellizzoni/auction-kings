# Task 05 — Implementar Queries: GetAuctionDetails, GetActiveAuctions, GetAuctionBids

## Contexto

As queries representam o lado de leitura do CQRS. Diferente dos commands, queries não alteram estado e podem usar caminhos otimizados (cache, projections, leitura direta). Cada query retorna DTOs (Data Transfer Objects) para desacoplar a representação da API do domain model interno.

## Arquivos a Alterar/Criar

- `src/services/AuctionKings.Application/Auctions/Queries/GetAuctionDetails/GetAuctionDetailsQuery.cs` — record com `AuctionId`
- `src/services/AuctionKings.Application/Auctions/Queries/GetAuctionDetails/GetAuctionDetailsQueryHandler.cs` — retorna `AuctionDetailDto` com dados do leilão + lista de bids
- `src/services/AuctionKings.Application/Auctions/Queries/GetActiveAuctions/GetActiveAuctionsQuery.cs` — record com `Page`, `PageSize`, filtros opcionais
- `src/services/AuctionKings.Application/Auctions/Queries/GetActiveAuctions/GetActiveAuctionsQueryHandler.cs` — retorna `PagedResult<AuctionSummaryDto>`
- `src/services/AuctionKings.Application/Auctions/Queries/GetAuctionBids/GetAuctionBidsQuery.cs` — record com `AuctionId`, `Page`, `PageSize`
- `src/services/AuctionKings.Application/Auctions/Queries/GetAuctionBids/GetAuctionBidsQueryHandler.cs` — retorna `PagedResult<BidDto>`
- `src/services/AuctionKings.Application/DTOs/AuctionDetailDto.cs` — `Id`, `Title`, `Description`, `CurrentPrice`, `StartTime`, `EndTime`, `Status`, `BidCount`, `TopBids`
- `src/services/AuctionKings.Application/DTOs/AuctionSummaryDto.cs` — versão resumida para listagem
- `src/services/AuctionKings.Application/DTOs/BidDto.cs` — `Id`, `BidderId`, `Amount`, `PlacedAt`, `Status`

## Critérios de Aceite

- `GetAuctionDetailsQueryHandler` busca auction por ID e retorna DTO completo; retorna `Result.Failure` se não encontrado
- `GetActiveAuctionsQueryHandler` retorna lista paginada de leilões ativos, ordenados por `EndTime` (mais próximo de fechar primeiro)
- `GetAuctionBidsQueryHandler` retorna bids paginados do leilão, ordenados por valor decrescente
- Queries consultam `ICacheService` primeiro, fallback para repositório
- DTOs são records imutáveis sem lógica de negócio

## Testes Obrigatórios

- `GetAuctionDetailsQueryHandler_ExistingAuction_ShouldReturnDto`
- `GetAuctionDetailsQueryHandler_NonExistent_ShouldReturnFailure`
- `GetActiveAuctionsQueryHandler_ShouldReturnPaginatedResults`
- `GetActiveAuctionsQueryHandler_NoActiveAuctions_ShouldReturnEmptyPage`
- `GetAuctionBidsQueryHandler_ShouldReturnBidsOrderedByAmount`

## Riscos/Observações

- Cache invalidation será implementado nos command handlers (Task 04) ou via domain event handlers — definir estratégia
- Para alto throughput, considerar read model separado no futuro (CQRS completo com projection)
- Filtros na query de listagem (categoria, faixa de preço) podem ser adicionados incrementalmente

## Estimativa

**M** (Medium)

## Dependências

- **Task 03** — interfaces `IAuctionRepository`, `ICacheService`, `PagedResult<T>`, DTOs base
- **Task 04** — commands criam os dados que as queries leem (necessário para testes de integração)

## Status

✅ **Completed** — GetAuctionDetailsQuery, GetActiveAuctionsQuery (with Redis cache), GetAuctionBidsQuery all implemented with DTOs (manual mapping, not Mapster). #29 (Mapster) remains open as it was not adopted.

## GitHub Issues

| #                                                                | Title                                               | Status    |
| ---------------------------------------------------------------- | --------------------------------------------------- | --------- |
| [#26](https://github.com/PellizzoniCode/auction-kings/issues/26) | Implementar GetAuctionQuery + ListAuctionsQuery     | ✅ closed |
| [#29](https://github.com/PellizzoniCode/auction-kings/issues/29) | Adicionar Mapster/AutoMapper para DTOs              | 🔴 open   |
