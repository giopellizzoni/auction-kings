# Task 07 — Implementar Outbox Processor e Idempotency

## Contexto

O Outbox Pattern garante consistência entre a persistência de aggregates e a publicação de domain events. O `OutboxProcessor` é um background service que periodicamente lê mensagens não processadas da tabela `outbox_messages` e as despacha (inicialmente via MediatR notifications, futuramente via SQS/SNS). A idempotência garante que commands duplicados (ex: retry de PlaceBid) não executem a operação duas vezes.

## Arquivos a Alterar/Criar

- `src/services/AuctionKings.Infrastructure/Outbox/OutboxProcessor.cs` — `BackgroundService` que faz polling na tabela `outbox_messages`, deserializa events, publica via `IPublisher` (MediatR), marca como processado
- `src/services/AuctionKings.Infrastructure/Outbox/OutboxOptions.cs` — configuração: polling interval, batch size, max retries
- `src/services/AuctionKings.Infrastructure/Persistence/Configurations/IdempotencyKeyConfiguration.cs` — tabela `idempotency_keys` com `Key`, `CreatedAt`, `ExpiresAt`
- `src/services/AuctionKings.Infrastructure/Persistence/IdempotencyKey.cs` — entity
- `src/services/AuctionKings.Application/Behaviors/IdempotencyBehavior.cs` — pipeline behavior que verifica se command já foi processado via idempotency key
- `src/services/AuctionKings.Application/Abstractions/IIdempotencyService.cs` — interface para verificar/registrar keys
- `src/services/AuctionKings.Infrastructure/Services/IdempotencyService.cs` — implementação com DbContext
- `src/services/AuctionKings.Domain/Events/DomainEventTypeRegistry.cs` — mapeamento type name ↔ CLR type para deserialização segura

## Critérios de Aceite

- `OutboxProcessor` processa mensagens em lotes configuráveis (default: 20)
- Mensagens com falha são retentadas até `MaxRetries` (default: 3), depois marcadas como `Failed`
- `IdempotencyBehavior` intercepta commands que implementam `IIdempotentCommand` e retorna resultado cacheado se key já existe
- `DomainEventTypeRegistry` mapeia nomes de tipos para tipos CLR sem usar `Type.GetType` (segurança)
- Outbox processor é resiliente a falhas (try/catch por mensagem, não para o lote inteiro)

## Testes Obrigatórios

- `OutboxProcessor_ShouldProcessPendingMessages`
- `OutboxProcessor_FailedMessage_ShouldRetry`
- `OutboxProcessor_MaxRetriesExceeded_ShouldMarkAsFailed`
- `IdempotencyBehavior_DuplicateCommand_ShouldReturnCachedResult`
- `IdempotencyBehavior_NewCommand_ShouldExecuteAndStoreKey`
- `DomainEventTypeRegistry_ShouldResolveKnownTypes`

## Riscos/Observações

- Polling interval do outbox afeta latência de eventos — 1s é bom para início, considerar change notification do PostgreSQL (LISTEN/NOTIFY) no futuro
- Cleanup de mensagens processadas deve ser agendado (não acumular indefinidamente)
- Idempotency keys devem expirar (TTL de 24h sugerido)
- O processor deve usar `FOR UPDATE SKIP LOCKED` para funcionar com múltiplas instâncias

## Estimativa

**L** (Large)

## Dependências

- **Task 06** — `OutboxMessage` entity, `AuctionDbContext` com tabela outbox
- **Task 03** — MediatR `IPublisher` para dispatch de events, pipeline behaviors

## Status

✅ **Completed** — OutboxProcessor (BackgroundService with batch processing, retries, SKIP LOCKED), IdempotencyBehavior (pipeline behavior), IdempotencyService, DomainEventTypeRegistry all implemented. UnitOfWork integrates outbox on every commit. Bus strategy is MediatR in-memory (#38 remains open for future SQS migration decision).

## GitHub Issues

| #                                                                | Title                                                       | Status    |
| ---------------------------------------------------------------- | ----------------------------------------------------------- | --------- |
| [#33](https://github.com/PellizzoniCode/auction-kings/issues/33) | Adicionar Idempotency (header X-Request-Id)                 | ✅ closed |
| [#34](https://github.com/PellizzoniCode/auction-kings/issues/34) | Adicionar tabela IdempotencyKeys + índice unique            | ✅ closed |
| [#35](https://github.com/PellizzoniCode/auction-kings/issues/35) | Criar Outbox table e modelo                                 | ✅ closed |
| [#36](https://github.com/PellizzoniCode/auction-kings/issues/36) | Integrar Outbox no PlaceBid (gravar evento BidPlaced)       | ✅ closed |
| [#37](https://github.com/PellizzoniCode/auction-kings/issues/37) | Implementar OutboxProcessor BackgroundService (SKIP LOCKED) | ✅ closed |
| [#38](https://github.com/PellizzoniCode/auction-kings/issues/38) | Escolher bus inicial: InMemory -> depois SQS                | ✅ closed |
