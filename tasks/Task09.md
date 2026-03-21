# Task 09 — Implementar SignalR Hubs, Notification Service e Connection Tracking

## Contexto

O sistema de tempo real é um diferencial da plataforma. Dois hubs SignalR estão planejados: `AuctionHub` para atualizações de leilão (novos bids, extensões, fechamento) e `NotificationHub` para notificações de usuário. As classes hub existem mas estão vazias e não herdam de `Hub`. O Redis backplane é necessário para deployments distribuídos. Um `ConnectionTracker` gerencia a associação user ↔ connections.

## Arquivos a Alterar/Criar

- `src/services/AuctionKings.API/AuctionKings.API.csproj` — adicionar project ref para Application (se não existir)
- `src/services/AuctionKings.API/Hubs/AuctionHub.cs` — herdar de `Hub`, implementar `JoinAuction(auctionId)`, `LeaveAuction(auctionId)`, `OnConnectedAsync`, `OnDisconnectedAsync`
- `src/services/AuctionKings.API/Hubs/NotificationHub.cs` — herdar de `Hub`, implementar `OnConnectedAsync` (join grupo do user)
- `src/services/AuctionKings.Infrastructure/Realtime/SignalRNotificationService.cs` — implementa `IRealtimeNotificationService`, usa `IHubContext<AuctionHub>` para enviar updates para grupos
- `src/services/AuctionKings.Infrastructure/Realtime/ConnectionTracker.cs` — tracking user ↔ connectionId usando Redis (para scale-out)
- `src/services/AuctionKings.API/Program.cs` — adicionar `AddSignalR().AddStackExchangeRedis()`, `MapHub<AuctionHub>("/hubs/auction")`, `MapHub<NotificationHub>("/hubs/notifications")`
- `Directory.Packages.props` — adicionar `Microsoft.AspNetCore.SignalR.StackExchangeRedis` se necessário

## Critérios de Aceite

- `AuctionHub.JoinAuction` adiciona connection ao grupo `auction-{id}`, `LeaveAuction` remove
- Quando um bid é feito, todos no grupo do leilão recebem update (via `SignalRNotificationService`)
- Quando leilão é fechado, todos no grupo recebem notificação de fechamento
- `ConnectionTracker` mantém mapa user → connectionIds em Redis (suporta múltiplas tabs/devices)
- Redis backplane configurado para funcionar com múltiplas instâncias da API
- `OnDisconnectedAsync` limpa tracking de connections
- Hubs aceitam apenas conexões autenticadas (quando auth estiver implementada — por enquanto aceitar anônimo)

## Testes Obrigatórios

- `AuctionHub_JoinAuction_ShouldAddToGroup`
- `AuctionHub_LeaveAuction_ShouldRemoveFromGroup`
- `SignalRNotificationService_BidPlaced_ShouldNotifyGroup`
- `SignalRNotificationService_AuctionClosed_ShouldNotifyGroup`
- `ConnectionTracker_Connect_ShouldTrackUser`
- `ConnectionTracker_Disconnect_ShouldRemoveConnection`

## Riscos/Observações

- SignalR com Redis backplane tem latência adicional (~1-5ms) — aceitável para auctions
- Reconexão automática no client-side precisa ser documentada (exponential backoff)
- Rate limiting de mensagens WebSocket (anti-flood) deve ser considerado
- Graceful shutdown: ao desligar a instância, notificar clients para reconectar a outra instância

## Estimativa

**L** (Large)

## Dependências

- **Task 03** — interface `IRealtimeNotificationService`
- **Task 08** — Redis configurado e disponível para backplane

## Status

⏳ **Not Started**

## GitHub Issues

| #                                                                | Title                                                         | Status  |
| ---------------------------------------------------------------- | ------------------------------------------------------------- | ------- |
| [#43](https://github.com/PellizzoniCode/auction-kings/issues/43) | Adicionar SignalR e criar AuctionHub                          | 🔴 open |
| [#44](https://github.com/PellizzoniCode/auction-kings/issues/44) | Broadcast de BidPlaced para grupo do leilão                   | 🔴 open |
| [#45](https://github.com/PellizzoniCode/auction-kings/issues/45) | Implementar state sync na reconexão                           | 🔴 open |
| [#46](https://github.com/PellizzoniCode/auction-kings/issues/46) | Configurar Redis backplane para SignalR (dev)                 | 🔴 open |
| [#47](https://github.com/PellizzoniCode/auction-kings/issues/47) | Teste manual e script simples para simular múltiplos clientes | 🔴 open |
