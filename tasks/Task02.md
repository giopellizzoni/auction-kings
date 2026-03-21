# Task 02 — Implementar Domain Model: Auction Aggregate, Bid Entity, Value Objects e Domain Events

## Contexto

O core do domínio é o aggregate `Auction` que controla o ciclo de vida de um leilão (criação, recebimento de lances, extensão automática, fechamento) e a entidade `Bid`. Toda a lógica de negócio de leilão deve residir aqui, sem dependências de infraestrutura. A spec define value objects (`Money`, `BidIncrement`, `AuctionDuration`), enums (`AuctionStatus`, `BidStatus`) e domain events que serão consumidos pelo Outbox Pattern.

## Arquivos a Alterar/Criar

- `src/services/AuctionKings.Domain/Aggregates/Auction.cs` — aggregate root com propriedades (`Title`, `Description`, `StartingPrice`, `CurrentPrice`, `StartTime`, `EndTime`, `Status`, `SellerId`, `WinnerId`, `Version`), métodos `PlaceBid()`, `Close()`, `Extend()`, regras de negócio inline
- `src/services/AuctionKings.Domain/Aggregates/Bid.cs` — entity com `AuctionId`, `BidderId`, `Amount`, `PlacedAt`, `Status`
- `src/services/AuctionKings.Domain/ValueObjects/Money.cs` — value object imutável com `Amount` (decimal) e `Currency` (string), operadores aritméticos
- `src/services/AuctionKings.Domain/ValueObjects/BidIncrement.cs` — regras de incremento mínimo por faixa de preço
- `src/services/AuctionKings.Domain/ValueObjects/AuctionDuration.cs` — encapsula duração com validação (mín/máx)
- `src/services/AuctionKings.Domain/Enums/AuctionStatus.cs` — `Pending`, `Active`, `Closed`, `Cancelled`
- `src/services/AuctionKings.Domain/Enums/BidStatus.cs` — `Active`, `Outbid`, `Winning`, `Won`, `Cancelled`
- `src/services/AuctionKings.Domain/Events/AuctionCreatedEvent.cs`
- `src/services/AuctionKings.Domain/Events/BidPlacedEvent.cs`
- `src/services/AuctionKings.Domain/Events/AuctionExtendedEvent.cs`
- `src/services/AuctionKings.Domain/Events/AuctionClosedEvent.cs`
- `src/services/AuctionKings.Domain/Events/WinnerDeterminedEvent.cs`
- `src/services/AuctionKings.Domain/Errors/DomainErrors.cs` — erros de domínio estáticos (`AuctionAlreadyClosed`, `BidTooLow`, `CannotBidOnOwnAuction`, etc.)
- Remover `src/services/AuctionKings.Domain/Class1.cs`

## Critérios de Aceite

- `Auction.PlaceBid()` valida: leilão ativo, bidder ≠ seller, valor > current price + incremento mínimo, retorna `Result<Bid>`
- `Auction.PlaceBid()` emite `BidPlacedEvent` e, se faltam < 5 min, emite `AuctionExtendedEvent` e estende `EndTime`
- `Auction.Close()` determina vencedor (último bid ativo), emite `AuctionClosedEvent` + `WinnerDeterminedEvent`
- `Money` impede valores negativos e moedas diferentes em operações
- Todos os domain events implementam `IDomainEvent`
- Aggregate não possui dependência de infraestrutura (sem EF Core, sem System.Data)

## Testes Obrigatórios

- `Auction_PlaceBid_ValidBid_ShouldSucceed`
- `Auction_PlaceBid_BelowMinimumIncrement_ShouldFail`
- `Auction_PlaceBid_OnClosedAuction_ShouldFail`
- `Auction_PlaceBid_BySeller_ShouldFail`
- `Auction_PlaceBid_NearEnd_ShouldExtendAuction`
- `Auction_Close_WithBids_ShouldDetermineWinner`
- `Auction_Close_NoBids_ShouldCloseWithoutWinner`
- `Money_Addition_DifferentCurrencies_ShouldThrow`
- `Money_Negative_ShouldThrow`
- `BidIncrement_ShouldReturnCorrectMinimumForPriceRange`

## Riscos/Observações

- A regra de extensão automática (anti-sniping) precisa ser configurável (tempo de extensão, threshold)
- Concorrência otimista será tratada na camada de infraestrutura via `Version` — o domain apenas incrementa
- `BidIncrement` com faixas de preço deve ser facilmente ajustável (considerar tabela estática vs configuração)

## Estimativa

**L** (Large)

## Dependências

- **Task 01** — `Entity`, `AggregateRoot`, `IDomainEvent`, `Result<T>`, `Error`

## Status

✅ **Completed**

## GitHub Issues

| # | Title | Status |
|---|-------|--------|
| [#9](https://github.com/PellizzoniCode/auction-kings/issues/9) | [EPIC] W1 Domínio (Modelagem e Regras) | ✅ closed |
| [#10](https://github.com/PellizzoniCode/auction-kings/issues/10) | Implementar entidades do domínio: Auction, Bid | ✅ closed |
| [#11](https://github.com/PellizzoniCode/auction-kings/issues/11) | Implementar Value Objects (Money, AuctionId, BidderId) | ✅ closed |
| [#12](https://github.com/PellizzoniCode/auction-kings/issues/12) | Implementar Result pattern + Domain Errors | ✅ closed |
| [#13](https://github.com/PellizzoniCode/auction-kings/issues/13) | Regras de negócio: place bid (validações) | ✅ closed |
| [#14](https://github.com/PellizzoniCode/auction-kings/issues/14) | Unit tests do Domínio (Auction/Bid/VOs) | ✅ closed |
