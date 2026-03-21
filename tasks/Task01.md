# Task 01 — Implementar Base Classes no Shared (Entity, AggregateRoot, Result)

## Contexto

O projeto segue Clean Architecture e DDD. As classes base `Entity` e `AggregateRoot` em `AuctionKings.Shared` estão vazias — sem `Id`, sem igualdade, sem coleção de domain events. Toda a camada de domínio depende dessas abstrações estarem corretas antes de qualquer aggregate ser implementado. Além disso, o padrão `Result<T>` / `Error` é necessário para retornos consistentes sem exceções de fluxo.

## Arquivos a Alterar/Criar

- `src/common/AuctionKings.Shared/Common/Entity.cs` — adicionar `Id` (Guid), override de `Equals`, `GetHashCode`, operadores `==`/`!=`
- `src/common/AuctionKings.Shared/Common/AggregateRoot.cs` — herdar de `Entity`, adicionar `_domainEvents` (list), métodos `AddDomainEvent`, `ClearDomainEvents`, `GetDomainEvents`
- `src/common/AuctionKings.Shared/Common/IDomainEvent.cs` — interface com `DateTime OccurredOn` e `Guid EventId`
- `src/common/AuctionKings.Shared/Common/Result.cs` — `Result` e `Result<T>` com `IsSuccess`, `IsFailure`, `Error`, factory methods `Success()`, `Failure(Error)`
- `src/common/AuctionKings.Shared/Common/Error.cs` — record com `Code` e `Message`
- `src/common/AuctionKings.Shared/Common/PagedResult.cs` — wrapper genérico com `Items`, `TotalCount`, `Page`, `PageSize`
- `src/common/AuctionKings.Shared/Common/IDateTimeProvider.cs` — interface para abstração de `DateTime.UtcNow`
- Remover `src/common/AuctionKings.Shared/Class1.cs`

## Critérios de Aceite

- `Entity` possui `Id` do tipo `Guid`, com igualdade baseada em `Id`
- `AggregateRoot` herda de `Entity` e gerencia coleção de `IDomainEvent`
- `Result<T>` é imutável e impede acesso a `Value` quando `IsFailure`
- `Error` é um record imutável com `Code` (string) e `Message` (string)
- `PagedResult<T>` calcula `HasNextPage` e `HasPreviousPage`
- Nenhuma dependência externa — apenas tipos do .NET
- Build do solution passa sem warnings

## Testes Obrigatórios

- `Entity_WithSameId_ShouldBeEqual`
- `Entity_WithDifferentId_ShouldNotBeEqual`
- `AggregateRoot_AddDomainEvent_ShouldStoreEvent`
- `AggregateRoot_ClearDomainEvents_ShouldRemoveAllEvents`
- `Result_Success_ShouldHaveValue`
- `Result_Failure_ShouldHaveError`
- `Result_Failure_AccessingValue_ShouldThrow`
- `PagedResult_ShouldCalculatePaginationCorrectly`

## Riscos/Observações

- O tipo de `Id` (Guid vs strongly-typed IDs) deve ser decidido aqui e mantido em todo o projeto
- `IDomainEvent` deve ser compatível com serialização JSON para o Outbox Pattern
- A assinatura de `ClearDomainEvents` deve retornar a lista limpa (para o UnitOfWork publicar antes de limpar)

## Estimativa

**S** (Small)

## Dependências

Nenhuma — esta é a tarefa fundacional.

## Status

✅ **Completed**

## GitHub Issues

| #                                                                | Title                                                      | Status    |
| ---------------------------------------------------------------- | ---------------------------------------------------------- | --------- |
| [#3](https://github.com/PellizzoniCode/auction-kings/issues/3)   | Criar solução e estrutura inicial de projetos (.NET)       | ✅ closed |
| [#4](https://github.com/PellizzoniCode/auction-kings/issues/4)   | Adicionar Directory.Build.props e Directory.Packages.props | ✅ closed |
| [#12](https://github.com/PellizzoniCode/auction-kings/issues/12) | Implementar Result pattern + Domain Errors                 | ✅ closed |
