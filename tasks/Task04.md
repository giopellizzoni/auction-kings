# Task 04 — Implementar Commands: CreateAuction, PlaceBid, CloseAuction

## Contexto

Com o pipeline MediatR e as interfaces definidos (Task 03), é hora de implementar os command handlers que representam as operações de escrita do sistema. Cada command segue o padrão: validação via FluentValidation → handler usa repositório → persiste via UnitOfWork → retorna `Result<T>`. Os handlers coordenam entre o domain model e a infraestrutura.

## Arquivos a Alterar/Criar

- `src/services/AuctionKings.Application/Auctions/Commands/CreateAuction/CreateAuctionCommand.cs` — record com `Title`, `Description`, `StartingPrice`, `Currency`, `Duration`, `SellerId`
- `src/services/AuctionKings.Application/Auctions/Commands/CreateAuction/CreateAuctionCommandHandler.cs` — cria aggregate, persiste, retorna `Result<Guid>`
- `src/services/AuctionKings.Application/Auctions/Commands/CreateAuction/CreateAuctionCommandValidator.cs` — validações: título não vazio (3-200 chars), preço > 0, duração válida
- `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommand.cs` — record com `AuctionId`, `BidderId`, `Amount`, `Currency`
- `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommandHandler.cs` — carrega auction, chama `PlaceBid()`, persiste, notifica em tempo real
- `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommandValidator.cs` — validações: IDs não vazios, amount > 0
- `src/services/AuctionKings.Application/Auctions/Commands/CloseAuction/CloseAuctionCommand.cs` — record com `AuctionId`
- `src/services/AuctionKings.Application/Auctions/Commands/CloseAuction/CloseAuctionCommandHandler.cs` — carrega auction, chama `Close()`, persiste
- `src/services/AuctionKings.Application/Auctions/Commands/CloseAuction/CloseAuctionCommandValidator.cs`

## Critérios de Aceite

- `CreateAuctionCommandHandler` cria `Auction` via factory method/construtor, salva no repositório, retorna `Result<Guid>` com ID
- `PlaceBidCommandHandler` carrega auction, delega `PlaceBid()` ao domain, salva, chama `IRealtimeNotificationService` para notificar sala do leilão
- `CloseAuctionCommandHandler` fecha o leilão e notifica participantes
- Todos os handlers usam `IUnitOfWork` para commit transacional
- Validators impedem dados inválidos antes de chegar ao handler
- Handlers retornam `Result<T>` (nunca lançam exceção para erros de negócio)

## Testes Obrigatórios

- `CreateAuctionCommandHandler_ValidCommand_ShouldCreateAuction`
- `CreateAuctionCommandHandler_InvalidTitle_ShouldFailValidation`
- `PlaceBidCommandHandler_ValidBid_ShouldSucceed`
- `PlaceBidCommandHandler_AuctionNotFound_ShouldReturnFailure`
- `PlaceBidCommandHandler_ShouldNotifyViaRealtime`
- `CloseAuctionCommandHandler_ActiveAuction_ShouldClose`
- `CloseAuctionCommandHandler_AlreadyClosed_ShouldReturnFailure`

## Riscos/Observações

- `PlaceBidCommandHandler` é o ponto de maior concorrência — retry com optimistic concurrency será tratado na infra (Task 07)
- Notificação em tempo real no handler é síncrona — se falhar, não deve impedir o lance (considerar fire-and-forget ou event-driven)
- Manter handlers finos — lógica de negócio fica no domain, handler é apenas orquestrador

## Estimativa

**M** (Medium)

## Dependências

- **Task 02** — domain model (Auction aggregate, Bid entity)
- **Task 03** — MediatR pipeline, interfaces de repositório, `IRealtimeNotificationService`

## Status

⏳ **Not Started**

## GitHub Issues

| # | Title | Status |
|---|-------|--------|
| [#22](https://github.com/PellizzoniCode/auction-kings/issues/22) | [EPIC] W2 Application Layer (MediatR, validação, pipeline) | 🔴 open |
| [#24](https://github.com/PellizzoniCode/auction-kings/issues/24) | Implementar CreateAuctionCommand + Handler | 🔴 open |
| [#25](https://github.com/PellizzoniCode/auction-kings/issues/25) | Implementar PlaceBidCommand + Handler | 🔴 open |
| [#32](https://github.com/PellizzoniCode/auction-kings/issues/32) | Implementar estratégia de retry para conflito de concorrência | 🔴 open |
