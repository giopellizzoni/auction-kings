# Task 13 — Implementar Background Workers (AuctionCloseWorker)

## Contexto

O `AuctionKings.BackgroundWorkers` é um projeto console separado que roda workers para processamento assíncrono. O worker mais crítico é o `AuctionCloseWorker`, que verifica periodicamente leilões cujo `EndTime` já passou e executa o fechamento. Atualmente o projeto contém apenas `Console.WriteLine("Hello, World!")`.

## Arquivos a Alterar/Criar

- `src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj` — adicionar `Microsoft.Extensions.Hosting`, project refs para Application, Infrastructure
- `src/workers/AuctionKings.BackgroundWorkers/Program.cs` — configurar `HostBuilder` com DI (Application + Infrastructure), logging, configuration
- `src/workers/AuctionKings.BackgroundWorkers/Workers/AuctionCloseWorker.cs` — `BackgroundService` que a cada N segundos busca leilões expirados e despacha `CloseAuctionCommand` via MediatR
- `src/workers/AuctionKings.BackgroundWorkers/Workers/CleanupExpiredDataWorker.cs` — `BackgroundService` que periodicamente limpa: idempotency keys expiradas, outbox messages processadas > 7 dias

## Critérios de Aceite

- `AuctionCloseWorker` executa a cada 10 segundos (configurável), busca leilões com `Status = Active` e `EndTime <= UtcNow`
- Usa `ISender` (MediatR) para despachar `CloseAuctionCommand` para cada leilão expirado
- Processamento individual — falha em um leilão não impede fechamento dos outros
- Worker é resiliente: try/catch por leilão, logging de erros, continua executando
- `CleanupExpiredDataWorker` roda a cada hora, limpa dados obsoletos
- Workers usam `IServiceScopeFactory` para criar scopes por iteração (evitar memory leaks)
- Graceful shutdown via `CancellationToken`

## Testes Obrigatórios

- `AuctionCloseWorker_ExpiredAuction_ShouldCloseViaCommand`
- `AuctionCloseWorker_NoExpiredAuctions_ShouldDoNothing`
- `AuctionCloseWorker_FailureOnOneAuction_ShouldContinueWithOthers`
- `CleanupExpiredDataWorker_ShouldDeleteOldOutboxMessages`
- `CleanupExpiredDataWorker_ShouldDeleteExpiredIdempotencyKeys`

## Riscos/Observações

- Em ambiente com múltiplas instâncias, o worker pode tentar fechar o mesmo leilão simultaneamente — usar lock distribuído (Redis) ou `SELECT ... FOR UPDATE SKIP LOCKED`
- O intervalo de polling afeta a precisão do fechamento — 10s significa até 10s de atraso
- Considerar usar Hangfire ou Quartz.NET para scheduling mais robusto no futuro
- Worker deve compartilhar a mesma configuração de DI (DbContext, MediatR) que a API

## Estimativa

**M** (Medium)

## Dependências

- **Task 04** — `CloseAuctionCommand` e handler
- **Task 06** — infraestrutura (DbContext, repositories) para queries de leilões expirados
- **Task 07** — Outbox processor (pode rodar no mesmo host ou separadamente)

## Status

🔄 **In Progress** — OutboxProcessor (BackgroundService) is implemented in the BackgroundWorkers project. AuctionCloseWorker and CleanupExpiredDataWorker are not yet implemented.

## GitHub Issues

| #                                                                | Title                                                       | Status    |
| ---------------------------------------------------------------- | ----------------------------------------------------------- | --------- |
| [#37](https://github.com/PellizzoniCode/auction-kings/issues/37) | Implementar OutboxProcessor BackgroundService (SKIP LOCKED) | ✅ closed |
