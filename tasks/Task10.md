# Task 10 — Implementar Controllers da API (Auctions, Bids, Users)

## Contexto

Os três controllers (`AuctionsController`, `BidsController`, `UsersControllers`) estão scaffoldados mas vazios — sem endpoints. Eles devem expor os use cases implementados nos commands e queries, mapeando HTTP requests para MediatR requests e retornando responses apropriados. Também é preciso corrigir o typo no nome `UsersControllers` → `UsersController`.

## Arquivos a Alterar/Criar

- `src/services/AuctionKings.API/Controllers/AuctionsController.cs` — endpoints: `POST /api/auctions` (criar), `GET /api/auctions` (listar ativos, paginado), `GET /api/auctions/{id}` (detalhes), `POST /api/auctions/{id}/close` (fechar)
- `src/services/AuctionKings.API/Controllers/BidsController.cs` — endpoints: `POST /api/auctions/{auctionId}/bids` (fazer lance), `GET /api/auctions/{auctionId}/bids` (listar bids do leilão)
- `src/services/AuctionKings.API/Controllers/UsersControllers.cs` → renomear para `UsersController.cs` — endpoints: `GET /api/users/{id}/bids` (histórico de bids do usuário)
- `src/services/AuctionKings.API/Program.cs` — garantir que MediatR, Application e Infrastructure DI estão registrados (`AddApplication()`, `AddInfrastructure()`)

## Critérios de Aceite

- Todos os endpoints usam `ISender` (MediatR) para despachar commands/queries
- Responses seguem padrão REST: `201 Created` (com Location header) para criação, `200 OK` para queries, `204 No Content` para close, `400 Bad Request` para validação, `404 Not Found` quando recurso não existe
- `Result<T>` do handler é mapeado para ActionResult apropriado (success → Ok/Created, failure → BadRequest/NotFound baseado no erro)
- Pagination via query params (`page`, `pageSize`) com defaults sensatos (page=1, pageSize=20, max=50)
- Endpoints documentados com atributos `[ProducesResponseType]` para Swagger
- Typo `UsersControllers` corrigido para `UsersController`

## Testes Obrigatórios

- `AuctionsController_CreateAuction_ShouldReturn201WithLocation`
- `AuctionsController_GetAuctions_ShouldReturnPaginatedList`
- `AuctionsController_GetAuctionById_NotFound_ShouldReturn404`
- `BidsController_PlaceBid_ShouldReturn201`
- `BidsController_PlaceBid_InvalidData_ShouldReturn400`
- `BidsController_GetBids_ShouldReturnPaginatedList`
- `UsersController_GetBidHistory_ShouldReturnPaginatedList`

## Riscos/Observações

- Autorização (quem pode fechar leilão, quem pode dar bid) será tratada em Task 11 — por ora endpoints são abertos
- Considerar API versioning (`/api/v1/`) desde o início para evitar breaking changes
- O `UserId` nos commands vem de onde? Inicialmente do body/route, depois do JWT claim quando auth for implementada
- Rate limiting no PlaceBid será feito via `[RateLimit]` attribute (Task 08)

## Estimativa

**M** (Medium)

## Dependências

- **Task 04** — commands (CreateAuction, PlaceBid, CloseAuction)
- **Task 05** — queries (GetAuctionDetails, GetActiveAuctions, GetAuctionBids)
- **Task 06** — infraestrutura registrada no DI

## Status

✅ **Done**

## GitHub Issues

| #                                                                | Title                                          | Status  |
| ---------------------------------------------------------------- | ---------------------------------------------- | ------- |
| [#19](https://github.com/PellizzoniCode/auction-kings/issues/19) | Criar endpoints REST básicos (Auctions)        | ✅ closed |
| [#20](https://github.com/PellizzoniCode/auction-kings/issues/20) | Criar endpoint de lance (sem real-time ainda)  | ✅ closed |
| [#21](https://github.com/PellizzoniCode/auction-kings/issues/21) | Adicionar OpenAPI/Swagger + versionamento (v1) | 🔴 open   |
