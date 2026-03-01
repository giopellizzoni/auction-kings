# Task 03 — Configurar Application Layer: MediatR, CQRS Pipeline Behaviors e Interfaces

## Contexto

A camada Application orquestra os use cases via CQRS (Command/Query Responsibility Segregation) usando MediatR. Antes de implementar handlers individuais, é preciso configurar o MediatR, definir as interfaces de repositório/serviços que a camada de infraestrutura implementará, e criar os pipeline behaviors (validação, logging, idempotência, performance). Atualmente `AuctionKings.Application` contém apenas `Class1.cs`.

## Arquivos a Alterar/Criar

- `src/services/AuctionKings.Application/AuctionKings.Application.csproj` — adicionar referências: `MediatR`, `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`, project ref para `AuctionKings.Domain` e `AuctionKings.Shared`
- `Directory.Packages.props` — adicionar versões de `MediatR`, `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`
- `src/services/AuctionKings.Application/DependencyInjection.cs` — extension method `AddApplication(this IServiceCollection)` registrando MediatR, validators, behaviors
- `src/services/AuctionKings.Application/Abstractions/IAuctionRepository.cs` — interface com `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `GetActiveAuctionsAsync`
- `src/services/AuctionKings.Application/Abstractions/IBidRepository.cs` — interface com `GetByAuctionIdAsync`, `AddAsync`
- `src/services/AuctionKings.Application/Abstractions/IUnitOfWork.cs` — interface com `SaveChangesAsync` e `CommitAsync`
- `src/services/AuctionKings.Application/Abstractions/IRealtimeNotificationService.cs` — interface para notificação SignalR
- `src/services/AuctionKings.Application/Abstractions/ICacheService.cs` — interface para cache Redis
- `src/services/AuctionKings.Application/Behaviors/ValidationBehavior.cs` — executa FluentValidation validators antes do handler
- `src/services/AuctionKings.Application/Behaviors/LoggingBehavior.cs` — loga request/response com correlation ID
- `src/services/AuctionKings.Application/Behaviors/PerformanceBehavior.cs` — loga warnings para handlers que excedem threshold (ex: 500ms)
- Remover `src/services/AuctionKings.Application/Class1.cs`

## Critérios de Aceite

- `AddApplication()` registra MediatR com assembly scanning do Application
- `ValidationBehavior` lança `ValidationException` customizada com lista de erros quando validação falha
- `LoggingBehavior` usa `ILogger` com structured logging (nome do request, tempo de execução)
- `PerformanceBehavior` emite warning quando execução > 500ms
- Interfaces de repositório são genéricas o suficiente para serem implementadas por EF Core
- Build do solution passa sem warnings

## Testes Obrigatórios

- `ValidationBehavior_WithInvalidRequest_ShouldThrowValidationException`
- `ValidationBehavior_WithValidRequest_ShouldCallNext`
- `LoggingBehavior_ShouldLogRequestAndResponse`
- `PerformanceBehavior_SlowRequest_ShouldLogWarning`

## Riscos/Observações

- A ordem dos pipeline behaviors importa: Logging → Validation → Performance → Handler
- `IUnitOfWork.CommitAsync` deve disparar domain events para o Outbox antes do commit da transação
- Não implementar `IdempotencyBehavior` aqui — será adicionado quando o Outbox estiver pronto (Task 07)

## Estimativa

**M** (Medium)

## Dependências

- **Task 01** — base classes no Shared
- **Task 02** — domain model (para types nas interfaces de repositório)
