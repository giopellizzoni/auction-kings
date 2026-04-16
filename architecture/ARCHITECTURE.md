# Sistema de Leiloes Real-Time - Auction Kings v2.0

## Sumario Executivo

Sistema de leiloes em tempo real construido com arquitetura moderna, escalavel e resiliente, utilizando .NET 10, AWS e praticas avancadas de engenharia de software. O sistema suporta milhares de usuarios simultaneos, garante consistencia de dados sob alta concorrencia e oferece latencia minima para atualizacoes em tempo real.

**Alteracoes da v2.0:**

- Correcao do controle de concorrencia otimista (agora a nivel de banco)
- Implementacao do Outbox Pattern para consistencia de eventos
- Persistencia de estado das Sagas
- Rate limiting distribuido com Redis
- Estrategia de snapshots para Event Sourcing
- Sincronizacao de estado no SignalR
- Health checks abrangentes
- Versionamento de API
- Gestao de segredos e tokens

---

## Visao Geral

### Stack Tecnologica

| Camada                 | Tecnologia                                    |
| ---------------------- | --------------------------------------------- |
| **Backend**            | .NET 10, ASP.NET Core 10                      |
| **Real-Time**          | SignalR (WebSockets) + Redis Backplane        |
| **Frontend**           | Blazor WebAssembly                            |
| **Autenticacao**       | AWS Cognito (User Pools + Identity Pools)     |
| **Database Principal** | Amazon RDS (PostgreSQL 16)                    |
| **Event Store**        | Amazon DynamoDB                               |
| **Cache**              | Amazon ElastiCache (Redis 7)                  |
| **Mensageria**         | Amazon SQS + SNS + EventBridge                |
| **Observabilidade**    | Prometheus + Grafana + AWS CloudWatch + X-Ray |
| **Infraestrutura**     | AWS (ECS Fargate, ALB, RDS, S3, CloudFront)   |
| **Secrets**            | AWS Secrets Manager                           |
| **Feature Flags**      | AWS AppConfig                                 |

### Funcionalidades Core

| Feature                      | Descricao                                                                                |
| ---------------------------- | ---------------------------------------------------------------------------------------- |
| **Sala de leilao real-time** | Sincronizacao instantanea via SignalR, broadcast eficiente, reconexao com sync de estado |
| **Submissao de lances**      | Validacao de regras, concorrencia otimista a nivel de DB, idempotencia garantida         |
| **Encerramento automatico**  | Timer distribuido com Redis, extensao automatica (anti-sniping)                          |
| **Definicao do vencedor**    | Algoritmo deterministico, auditoria completa, notificacao multi-canal                    |
| **Historico e auditoria**    | Event Sourcing com snapshots, armazenamento imutavel                                     |
| **Notificacoes**             | SignalR + SNS para push/email/SMS                                                        |

---

## Arquitetura

### Diagrama de Alto Nivel

```text
                                    +------------------+
                                    |   CloudFront     |
                                    |   (CDN + Edge)   |
                                    +--------+---------+
                                             |
                                    +--------v---------+
                                    |       ALB        |
                                    | (Load Balancer)  |
                                    +--------+---------+
                                             |
                    +------------------------+------------------------+
                    |                        |                        |
           +--------v--------+     +---------v-------+      +---------v-------+
           |   API Service   |     | SignalR Service |      |  Worker Service |
           |   (ECS Fargate) |     | (ECS Fargate)   |      |  (ECS Fargate)  |
           +--------+--------+     +---------+-------+      +---------+-------+
                    |                        |                        |
                    +------------+-----------+------------+-----------+
                                 |                        |
                    +------------v-----------+  +---------v-----------+
                    |      Redis Cluster     |  |    SQS Queues       |
                    | (Cache + SignalR BP +  |  | (Commands + Events) |
                    |  Distributed Locks)    |  +---------+-----------+
                    +------------+-----------+            |
                                 |                        |
                    +------------v-----------+  +---------v-----------+
                    |     PostgreSQL RDS     |  |      DynamoDB       |
                    |  (Write Model + Read   |  |   (Event Store)     |
                    |   Model + Outbox)      |  +---------------------+
                    +------------------------+
```

### Padroes Arquiteturais

#### 1. Clean Architecture + Ports and Adapters

```
+------------------------------------------------------------------+
|                      Presentation Layer                           |
|            (Controllers, SignalR Hubs, Blazor Pages)              |
+------------------------------------------------------------------+
                                |
                                v
+------------------------------------------------------------------+
|                      Application Layer                            |
|        (Commands, Queries, Handlers, DTOs, Behaviors)             |
|                    Ports (Interfaces)                             |
+------------------------------------------------------------------+
                                |
                                v
+------------------------------------------------------------------+
|                        Domain Layer                               |
|    (Aggregates, Entities, Value Objects, Domain Events,           |
|     Domain Services, Specifications)                              |
+------------------------------------------------------------------+
                                ^
                                |
+------------------------------------------------------------------+
|                     Infrastructure Layer                          |
|              Adapters (Implementations)                           |
|    (Repositories, External Services, Messaging, Persistence)      |
+------------------------------------------------------------------+
```

#### 2. CQRS (Command Query Responsibility Segregation)

**Commands (Write Model):**

- `PlaceBidCommand` - Submeter lance
- `CreateAuctionCommand` - Criar leilao
- `CloseAuctionCommand` - Fechar leilao
- `CancelBidCommand` - Cancelar lance (admin)

**Queries (Read Model):**

- `GetAuctionDetailsQuery` - Detalhes do leilao
- `GetAuctionBidsQuery` - Historico de lances
- `GetUserBidHistoryQuery` - Lances do usuario
- `GetActiveAuctionsQuery` - Leiloes ativos

#### 3. Event Sourcing com Snapshots

```
+-------------------+     +-------------------+     +-------------------+
|   Command         | --> |   Aggregate       | --> |   Domain Events   |
|   (PlaceBid)      |     |   (Auction)       |     |   (BidPlaced)     |
+-------------------+     +-------------------+     +-------------------+
                                                             |
                          +----------------------------------+
                          |                                  |
                          v                                  v
                 +------------------+              +------------------+
                 |   Event Store    |              |   Outbox Table   |
                 |   (DynamoDB)     |              |   (PostgreSQL)   |
                 +------------------+              +------------------+
                          |                                  |
                          v                                  v
                 +------------------+              +------------------+
                 |   Snapshots      |              |   Event Publisher|
                 |   (Every 50 ev)  |              |   (Background)   |
                 +------------------+              +------------------+
```

---

## Desafios Tecnicos e Solucoes

### 1. Concorrencia e Race Conditions

**Desafio**: Multiplos usuarios fazendo lances simultaneos no mesmo leilao.

**Solucao**: Concorrencia otimista a nivel de banco de dados.

```csharp
// Domain/Aggregates/Auction/Auction.cs
public class Auction : AggregateRoot
{
    public Guid Id { get; private set; }
    public decimal CurrentPrice { get; private set; }
    public Guid? CurrentWinnerId { get; private set; }
    public AuctionStatus Status { get; private set; }
    public DateTime EndTime { get; private set; }
    public bool AutoExtendEnabled { get; private set; }

    // Concurrency token - managed by EF Core/PostgreSQL
    public uint Version { get; private set; }

    private readonly List<Bid> _bids = new();
    public IReadOnlyCollection<Bid> Bids => _bids.AsReadOnly();

    public Result<Bid> PlaceBid(Guid userId, decimal amount, Guid requestId)
    {
        // Business rule validations
        if (Status != AuctionStatus.Active)
            return Result.Failure<Bid>(DomainErrors.Auction.NotActive);

        if (DateTime.UtcNow >= EndTime)
            return Result.Failure<Bid>(DomainErrors.Auction.Ended);

        if (amount <= CurrentPrice)
            return Result.Failure<Bid>(DomainErrors.Bid.AmountTooLow(CurrentPrice));

        if (userId == CurrentWinnerId)
            return Result.Failure<Bid>(DomainErrors.Bid.AlreadyWinning);

        // Create bid
        var bid = new Bid(
            id: Guid.NewGuid(),
            auctionId: Id,
            userId: userId,
            amount: amount,
            requestId: requestId,
            createdAt: DateTime.UtcNow
        );

        _bids.Add(bid);
        CurrentPrice = amount;
        CurrentWinnerId = userId;

        // Auto-extension (anti-sniping)
        if (AutoExtendEnabled && EndTime - DateTime.UtcNow < TimeSpan.FromSeconds(30))
        {
            EndTime = EndTime.AddMinutes(2);
            AddDomainEvent(new AuctionExtendedEvent(Id, EndTime));
        }

        // Domain event - will be persisted via Outbox
        AddDomainEvent(new BidPlacedEvent(
            bid.Id, Id, userId, amount, CurrentPrice, DateTime.UtcNow));

        return Result.Success(bid);
    }
}
```

```csharp
// Infrastructure/Persistence/Configurations/AuctionConfiguration.cs
public class AuctionConfiguration : IEntityTypeConfiguration<Auction>
{
    public void Configure(EntityTypeBuilder<Auction> builder)
    {
        builder.ToTable("auctions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.CurrentPrice)
            .HasColumnName("current_price")
            .HasPrecision(18, 2);

        // PostgreSQL xmin for optimistic concurrency
        builder.Property(a => a.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasMany(a => a.Bids)
            .WithOne()
            .HasForeignKey(b => b.AuctionId);

        // Indexes
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => new { a.Status, a.EndTime })
            .HasFilter("status = 'Active'");
    }
}
```

```csharp
// Application/Commands/Bids/PlaceBidCommandHandler.cs
public class PlaceBidCommandHandler : ICommandHandler<PlaceBidCommand, BidDto>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PlaceBidCommandHandler> _logger;

    public async Task<Result<BidDto>> Handle(
        PlaceBidCommand command,
        CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(command.AuctionId, cancellationToken);

        if (auction is null)
            return Result.Failure<BidDto>(DomainErrors.Auction.NotFound);

        var bidResult = auction.PlaceBid(command.UserId, command.Amount, command.RequestId);

        if (bidResult.IsFailure)
            return Result.Failure<BidDto>(bidResult.Error);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(BidDto.FromBid(bidResult.Value));
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "Concurrent bid detected for Auction {AuctionId} by User {UserId}",
                command.AuctionId, command.UserId);

            return Result.Failure<BidDto>(DomainErrors.Bid.ConcurrentModification);
        }
    }
}
```

### 2. Idempotencia

**Desafio**: Evitar lances duplicados por retry de rede ou multiplos cliques.

**Solucao**: Idempotency key com cache distribuido + verificacao em banco.

```csharp
// Application/Behaviors/IdempotencyBehavior.cs
public class IdempotencyBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentCommand<TResponse>
{
    private readonly IDistributedCache _cache;
    private readonly IIdempotencyStore _store;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = request.GetIdempotencyKey();
        var cacheKey = $"idempotency:{typeof(TRequest).Name}:{idempotencyKey}";

        // 1. Check cache first (fast path)
        var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cachedResult is not null)
        {
            _logger.LogDebug("Idempotent request found in cache: {Key}", idempotencyKey);
            return JsonSerializer.Deserialize<TResponse>(cachedResult)!;
        }

        // 2. Check persistent store (in case cache was evicted)
        var storedResult = await _store.GetAsync<TResponse>(idempotencyKey, cancellationToken);
        if (storedResult is not null)
        {
            // Repopulate cache
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(storedResult),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheDuration
                },
                cancellationToken);

            return storedResult;
        }

        // 3. Process request
        var response = await next();

        // 4. Store result
        var serializedResponse = JsonSerializer.Serialize(response);

        await Task.WhenAll(
            _cache.SetStringAsync(
                cacheKey,
                serializedResponse,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheDuration
                },
                cancellationToken),
            _store.SetAsync(idempotencyKey, response, CacheDuration, cancellationToken)
        );

        return response;
    }
}
```

```csharp
// Application/Commands/Bids/PlaceBidCommand.cs
public record PlaceBidCommand(
    Guid AuctionId,
    Guid UserId,
    decimal Amount,
    Guid RequestId  // Client-generated idempotency key
) : IIdempotentCommand<Result<BidDto>>
{
    public string GetIdempotencyKey() => $"{UserId}:{AuctionId}:{RequestId}";
}
```

### 3. Consistencia com Outbox Pattern

**Desafio**: Garantir que eventos de dominio sejam publicados mesmo em caso de falhas.

**Solucao**: Outbox Pattern - salvar eventos na mesma transacao do estado.

```csharp
// Infrastructure/Persistence/OutboxMessage.cs
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}
```

```csharp
// Infrastructure/Persistence/UnitOfWork.cs
public class UnitOfWork : IUnitOfWork
{
    private readonly AuctionDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect domain events from all tracked aggregates
        var aggregatesWithEvents = _context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = aggregatesWithEvents
            .SelectMany(a => a.DomainEvents)
            .ToList();

        // Convert to outbox messages
        var outboxMessages = domainEvents.Select(e => new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = e.GetType().AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(e, e.GetType()),
            CreatedAt = _dateTimeProvider.UtcNow
        }).ToList();

        // Add outbox messages to context
        await _context.OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);

        // Save everything in single transaction
        await _context.SaveChangesAsync(cancellationToken);

        // Clear domain events after successful save
        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }
    }
}
```

```csharp
// Infrastructure/BackgroundServices/OutboxProcessor.cs
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        // Get unprocessed messages with row-level locking
        var messages = await context.OutboxMessages
            .FromSqlRaw(@"
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL AND retry_count < 5
                ORDER BY created_at
                LIMIT 100
                FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.Type);
                if (eventType is null)
                {
                    _logger.LogWarning("Unknown event type: {Type}", message.Type);
                    message.ProcessedAt = DateTime.UtcNow;
                    message.Error = "Unknown event type";
                    continue;
                }

                var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType);

                await publisher.PublishAsync(domainEvent!, cancellationToken);

                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {Id}", message.Id);
                message.RetryCount++;
                message.Error = ex.Message;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
```

### 4. Event Sourcing com Snapshots

**Desafio**: Reconstruir estado a partir de milhares de eventos e impacto na performance.

**Solucao**: Snapshots periodicos + eventos incrementais.

```csharp
// Infrastructure/EventStore/IEventStore.cs
public interface IEventStore
{
    Task AppendEventsAsync(
        string streamId,
        IEnumerable<IDomainEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IDomainEvent>> GetEventsAsync(
        string streamId,
        int fromVersion = 0,
        CancellationToken cancellationToken = default);

    Task SaveSnapshotAsync<T>(
        string streamId,
        T snapshot,
        int version,
        CancellationToken cancellationToken = default) where T : class;

    Task<(T? Snapshot, int Version)> GetLatestSnapshotAsync<T>(
        string streamId,
        CancellationToken cancellationToken = default) where T : class;
}
```

```csharp
// Infrastructure/EventStore/DynamoDbEventStore.cs
public class DynamoDbEventStore : IEventStore
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<DynamoDbEventStore> _logger;
    private const string EventsTable = "AuctionEvents";
    private const string SnapshotsTable = "AuctionSnapshots";
    private const int SnapshotFrequency = 50;

    public async Task AppendEventsAsync(
        string streamId,
        IEnumerable<IDomainEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var eventsList = events.ToList();
        var currentVersion = expectedVersion;

        foreach (var @event in eventsList)
        {
            currentVersion++;

            var item = new Dictionary<string, AttributeValue>
            {
                ["StreamId"] = new() { S = streamId },
                ["Version"] = new() { N = currentVersion.ToString() },
                ["EventType"] = new() { S = @event.GetType().Name },
                ["EventData"] = new() { S = JsonSerializer.Serialize(@event, @event.GetType()) },
                ["Timestamp"] = new() { N = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() },
                ["EventId"] = new() { S = Guid.NewGuid().ToString() }
            };

            var request = new PutItemRequest
            {
                TableName = EventsTable,
                Item = item,
                // Optimistic concurrency - fail if version already exists
                ConditionExpression = "attribute_not_exists(StreamId) AND attribute_not_exists(Version)"
            };

            try
            {
                await _dynamoDb.PutItemAsync(request, cancellationToken);
            }
            catch (ConditionalCheckFailedException)
            {
                throw new ConcurrencyException(
                    $"Concurrent write detected for stream {streamId} at version {currentVersion}");
            }
        }

        // Check if snapshot is needed
        if (currentVersion % SnapshotFrequency == 0)
        {
            _logger.LogInformation(
                "Snapshot threshold reached for stream {StreamId} at version {Version}",
                streamId, currentVersion);
        }
    }

    public async Task<IReadOnlyList<IDomainEvent>> GetEventsAsync(
        string streamId,
        int fromVersion = 0,
        CancellationToken cancellationToken = default)
    {
        var request = new QueryRequest
        {
            TableName = EventsTable,
            KeyConditionExpression = "StreamId = :streamId AND Version > :version",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":streamId"] = new() { S = streamId },
                [":version"] = new() { N = fromVersion.ToString() }
            },
            ScanIndexForward = true // Ascending order
        };

        var response = await _dynamoDb.QueryAsync(request, cancellationToken);

        return response.Items
            .Select(item => DeserializeEvent(item))
            .ToList();
    }

    public async Task SaveSnapshotAsync<T>(
        string streamId,
        T snapshot,
        int version,
        CancellationToken cancellationToken = default) where T : class
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["StreamId"] = new() { S = streamId },
            ["Version"] = new() { N = version.ToString() },
            ["SnapshotType"] = new() { S = typeof(T).AssemblyQualifiedName! },
            ["SnapshotData"] = new() { S = JsonSerializer.Serialize(snapshot) },
            ["CreatedAt"] = new() { N = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() }
        };

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = SnapshotsTable,
            Item = item
        }, cancellationToken);
    }

    public async Task<(T? Snapshot, int Version)> GetLatestSnapshotAsync<T>(
        string streamId,
        CancellationToken cancellationToken = default) where T : class
    {
        var request = new QueryRequest
        {
            TableName = SnapshotsTable,
            KeyConditionExpression = "StreamId = :streamId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":streamId"] = new() { S = streamId }
            },
            ScanIndexForward = false, // Descending - latest first
            Limit = 1
        };

        var response = await _dynamoDb.QueryAsync(request, cancellationToken);

        if (!response.Items.Any())
            return (null, 0);

        var item = response.Items.First();
        var version = int.Parse(item["Version"].N);
        var snapshot = JsonSerializer.Deserialize<T>(item["SnapshotData"].S);

        return (snapshot, version);
    }

    private IDomainEvent DeserializeEvent(Dictionary<string, AttributeValue> item)
    {
        var eventType = item["EventType"].S;
        var eventData = item["EventData"].S;

        var type = DomainEventTypeRegistry.GetType(eventType);
        return (IDomainEvent)JsonSerializer.Deserialize(eventData, type)!;
    }
}
```

```csharp
// Domain/Common/DomainEventTypeRegistry.cs
public static class DomainEventTypeRegistry
{
    private static readonly Dictionary<string, Type> EventTypes = new();

    static DomainEventTypeRegistry()
    {
        // Register all domain events
        var eventTypes = typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in eventTypes)
        {
            EventTypes[type.Name] = type;
        }
    }

    public static Type GetType(string eventTypeName)
    {
        if (EventTypes.TryGetValue(eventTypeName, out var type))
            return type;

        throw new InvalidOperationException($"Unknown event type: {eventTypeName}");
    }
}
```

```csharp
// Infrastructure/Repositories/EventSourcedAuctionRepository.cs
public class EventSourcedAuctionRepository : IEventSourcedAuctionRepository
{
    private readonly IEventStore _eventStore;
    private readonly IAuctionSnapshotService _snapshotService;

    public async Task<Auction> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var streamId = $"auction-{id}";

        // 1. Try to load from snapshot
        var (snapshot, snapshotVersion) = await _eventStore
            .GetLatestSnapshotAsync<AuctionSnapshot>(streamId, cancellationToken);

        // 2. Load events after snapshot (or all events if no snapshot)
        var events = await _eventStore.GetEventsAsync(
            streamId,
            snapshotVersion,
            cancellationToken);

        // 3. Rebuild aggregate
        var auction = snapshot is not null
            ? Auction.FromSnapshot(snapshot)
            : new Auction();

        foreach (var @event in events)
        {
            auction.Apply(@event);
        }

        return auction;
    }

    public async Task SaveAsync(Auction auction, CancellationToken cancellationToken)
    {
        var streamId = $"auction-{auction.Id}";
        var events = auction.DomainEvents.ToList();

        await _eventStore.AppendEventsAsync(
            streamId,
            events,
            auction.Version - events.Count,
            cancellationToken);

        // Create snapshot if needed
        if (auction.Version % 50 == 0)
        {
            var snapshot = auction.ToSnapshot();
            await _eventStore.SaveSnapshotAsync(streamId, snapshot, auction.Version, cancellationToken);
        }

        auction.ClearDomainEvents();
    }
}
```

### 5. Saga Pattern com Persistencia de Estado

**Desafio**: Coordenar processos distribuidos (fechar leilao, processar pagamento, notificar).

**Solucao**: Saga com estado persistido e compensacao.

```csharp
// Domain/Sagas/AuctionClosingSaga.cs
public class AuctionClosingSagaState
{
    public Guid SagaId { get; set; }
    public Guid AuctionId { get; set; }
    public SagaStatus Status { get; set; }
    public int CurrentStep { get; set; }
    public Guid? WinnerId { get; set; }
    public decimal? WinningAmount { get; set; }
    public string? PaymentReservationId { get; set; }
    public List<string> CompletedSteps { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

public enum SagaStatus
{
    Started,
    DeterminingWinner,
    ReservingPayment,
    ReservingItem,
    NotifyingParticipants,
    Completed,
    Compensating,
    Failed
}
```

```csharp
// Application/Sagas/AuctionClosingSagaHandler.cs
public class AuctionClosingSagaHandler
{
    private readonly ISagaStateRepository _sagaRepository;
    private readonly IAuctionService _auctionService;
    private readonly IPaymentService _paymentService;
    private readonly IInventoryService _inventoryService;
    private readonly INotificationService _notificationService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<AuctionClosingSagaHandler> _logger;

    public async Task HandleAsync(AuctionClosingStartedEvent @event, CancellationToken ct)
    {
        var sagaState = new AuctionClosingSagaState
        {
            SagaId = Guid.NewGuid(),
            AuctionId = @event.AuctionId,
            Status = SagaStatus.Started,
            StartedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };

        await _sagaRepository.SaveAsync(sagaState, ct);

        try
        {
            // Step 1: Determine winner
            sagaState.Status = SagaStatus.DeterminingWinner;
            await _sagaRepository.SaveAsync(sagaState, ct);

            var winner = await _auctionService.DetermineWinnerAsync(@event.AuctionId, ct);
            if (winner is null)
            {
                sagaState.Status = SagaStatus.Completed;
                sagaState.CompletedAt = DateTime.UtcNow;
                sagaState.CompletedSteps.Add("NoWinner");
                await _sagaRepository.SaveAsync(sagaState, ct);
                return;
            }

            sagaState.WinnerId = winner.UserId;
            sagaState.WinningAmount = winner.Amount;
            sagaState.CompletedSteps.Add("WinnerDetermined");
            await _sagaRepository.SaveAsync(sagaState, ct);

            // Step 2: Reserve payment
            sagaState.Status = SagaStatus.ReservingPayment;
            await _sagaRepository.SaveAsync(sagaState, ct);

            var paymentResult = await _paymentService.ReservePaymentAsync(
                winner.UserId, winner.Amount, ct);

            if (paymentResult.IsFailure)
            {
                throw new SagaStepFailedException("PaymentReservation", paymentResult.Error);
            }

            sagaState.PaymentReservationId = paymentResult.Value.ReservationId;
            sagaState.CompletedSteps.Add("PaymentReserved");
            await _sagaRepository.SaveAsync(sagaState, ct);

            // Step 3: Reserve item
            sagaState.Status = SagaStatus.ReservingItem;
            await _sagaRepository.SaveAsync(sagaState, ct);

            var itemResult = await _inventoryService.ReserveItemAsync(
                @event.ItemId, winner.UserId, ct);

            if (itemResult.IsFailure)
            {
                throw new SagaStepFailedException("ItemReservation", itemResult.Error);
            }

            sagaState.CompletedSteps.Add("ItemReserved");
            await _sagaRepository.SaveAsync(sagaState, ct);

            // Step 4: Notify participants
            sagaState.Status = SagaStatus.NotifyingParticipants;
            await _sagaRepository.SaveAsync(sagaState, ct);

            await _notificationService.NotifyWinnerAsync(winner, ct);
            await _notificationService.NotifyLosersAsync(@event.AuctionId, winner.UserId, ct);
            await _notificationService.NotifySellerAsync(@event.AuctionId, winner, ct);

            sagaState.CompletedSteps.Add("NotificationsSent");

            // Complete saga
            sagaState.Status = SagaStatus.Completed;
            sagaState.CompletedAt = DateTime.UtcNow;
            await _sagaRepository.SaveAsync(sagaState, ct);

            await _eventPublisher.PublishAsync(
                new AuctionClosingSagaCompletedEvent(sagaState.SagaId, @event.AuctionId), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga {SagaId} failed at step {Step}",
                sagaState.SagaId, sagaState.Status);

            sagaState.ErrorMessage = ex.Message;
            await CompensateAsync(sagaState, ct);
        }
    }

    private async Task CompensateAsync(AuctionClosingSagaState state, CancellationToken ct)
    {
        state.Status = SagaStatus.Compensating;
        await _sagaRepository.SaveAsync(state, ct);

        var compensationErrors = new List<string>();

        // Compensate in reverse order
        if (state.CompletedSteps.Contains("ItemReserved"))
        {
            try
            {
                await _inventoryService.ReleaseItemAsync(state.AuctionId, ct);
            }
            catch (Exception ex)
            {
                compensationErrors.Add($"ItemRelease: {ex.Message}");
            }
        }

        if (state.CompletedSteps.Contains("PaymentReserved") &&
            state.PaymentReservationId is not null)
        {
            try
            {
                await _paymentService.ReleaseReservationAsync(state.PaymentReservationId, ct);
            }
            catch (Exception ex)
            {
                compensationErrors.Add($"PaymentRelease: {ex.Message}");
            }
        }

        if (state.CompletedSteps.Contains("WinnerDetermined"))
        {
            try
            {
                await _auctionService.ReopenAuctionAsync(state.AuctionId, ct);
            }
            catch (Exception ex)
            {
                compensationErrors.Add($"AuctionReopen: {ex.Message}");
            }
        }

        state.Status = compensationErrors.Any() ? SagaStatus.Failed : SagaStatus.Compensating;
        state.ErrorMessage = string.Join("; ", compensationErrors);
        state.CompletedAt = DateTime.UtcNow;
        await _sagaRepository.SaveAsync(state, ct);

        await _eventPublisher.PublishAsync(
            new AuctionClosingSagaFailedEvent(state.SagaId, state.AuctionId, state.ErrorMessage), ct);
    }
}
```

### 6. SignalR com Sincronizacao de Estado

**Desafio**: Manter clientes sincronizados, lidar com reconexoes e escalar horizontalmente.

**Solucao**: Redis backplane + sincronizacao de estado na reconexao.

```csharp
// Program.cs - SignalR Configuration
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
    options.StreamBufferCapacity = 10;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
})
.AddStackExchangeRedis(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")!;
    options.Configuration.ChannelPrefix = RedisChannel.Literal("AuctionKings");
})
.AddMessagePackProtocol(); // Binary serialization for performance
```

```csharp
// API/Hubs/AuctionHub.cs
[Authorize]
public class AuctionHub : Hub
{
    private readonly IAuctionReadService _auctionService;
    private readonly IConnectionTracker _connectionTracker;
    private readonly ILogger<AuctionHub> _logger;

    public AuctionHub(
        IAuctionReadService auctionService,
        IConnectionTracker connectionTracker,
        ILogger<AuctionHub> logger)
    {
        _auctionService = auctionService;
        _connectionTracker = connectionTracker;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        var connectionId = Context.ConnectionId;

        await _connectionTracker.TrackConnectionAsync(userId, connectionId);

        _logger.LogInformation(
            "Client connected: {ConnectionId}, User: {UserId}",
            connectionId, userId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        var connectionId = Context.ConnectionId;

        await _connectionTracker.RemoveConnectionAsync(userId, connectionId);

        // Leave all auction groups
        var groups = await _connectionTracker.GetUserGroupsAsync(connectionId);
        foreach (var group in groups)
        {
            await Groups.RemoveFromGroupAsync(connectionId, group);
        }

        _logger.LogInformation(
            "Client disconnected: {ConnectionId}, User: {UserId}, Exception: {Exception}",
            connectionId, userId, exception?.Message);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinAuction(Guid auctionId)
    {
        var groupName = GetAuctionGroupName(auctionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await _connectionTracker.AddToGroupAsync(Context.ConnectionId, groupName);

        // Send current state to newly joined client
        var auctionState = await _auctionService.GetAuctionStateAsync(auctionId);

        await Clients.Caller.SendAsync("AuctionState", new AuctionStateDto
        {
            AuctionId = auctionId,
            CurrentPrice = auctionState.CurrentPrice,
            CurrentWinnerId = auctionState.CurrentWinnerId,
            CurrentWinnerName = auctionState.CurrentWinnerName,
            BidCount = auctionState.BidCount,
            EndTime = auctionState.EndTime,
            Status = auctionState.Status,
            RecentBids = auctionState.RecentBids.Take(10).ToList(),
            ServerTime = DateTime.UtcNow,
            Version = auctionState.Version
        });

        _logger.LogDebug(
            "Client {ConnectionId} joined auction {AuctionId}",
            Context.ConnectionId, auctionId);
    }

    public async Task LeaveAuction(Guid auctionId)
    {
        var groupName = GetAuctionGroupName(auctionId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await _connectionTracker.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug(
            "Client {ConnectionId} left auction {AuctionId}",
            Context.ConnectionId, auctionId);
    }

    public async Task RequestStateSync(Guid auctionId, int clientVersion)
    {
        var auctionState = await _auctionService.GetAuctionStateAsync(auctionId);

        if (auctionState.Version > clientVersion)
        {
            // Client is behind, send full state
            await Clients.Caller.SendAsync("AuctionState", new AuctionStateDto
            {
                AuctionId = auctionId,
                CurrentPrice = auctionState.CurrentPrice,
                CurrentWinnerId = auctionState.CurrentWinnerId,
                CurrentWinnerName = auctionState.CurrentWinnerName,
                BidCount = auctionState.BidCount,
                EndTime = auctionState.EndTime,
                Status = auctionState.Status,
                RecentBids = auctionState.RecentBids.Take(10).ToList(),
                ServerTime = DateTime.UtcNow,
                Version = auctionState.Version
            });
        }
    }

    private static string GetAuctionGroupName(Guid auctionId) => $"auction:{auctionId}";
}
```

```csharp
// Infrastructure/SignalR/SignalRNotificationService.cs
public class SignalRNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<AuctionHub> _hubContext;

    public async Task BroadcastBidPlacedAsync(BidPlacedEvent @event)
    {
        var groupName = $"auction:{@event.AuctionId}";

        await _hubContext.Clients.Group(groupName).SendAsync("BidPlaced", new BidPlacedDto
        {
            BidId = @event.BidId,
            AuctionId = @event.AuctionId,
            UserId = @event.UserId,
            Amount = @event.Amount,
            NewCurrentPrice = @event.NewCurrentPrice,
            Timestamp = @event.Timestamp,
            Version = @event.Version
        });
    }

    public async Task BroadcastAuctionEndingSoonAsync(Guid auctionId, TimeSpan timeRemaining)
    {
        var groupName = $"auction:{auctionId}";

        await _hubContext.Clients.Group(groupName).SendAsync("AuctionEndingSoon", new
        {
            AuctionId = auctionId,
            SecondsRemaining = (int)timeRemaining.TotalSeconds
        });
    }

    public async Task BroadcastAuctionExtendedAsync(Guid auctionId, DateTime newEndTime)
    {
        var groupName = $"auction:{auctionId}";

        await _hubContext.Clients.Group(groupName).SendAsync("AuctionExtended", new
        {
            AuctionId = auctionId,
            NewEndTime = newEndTime
        });
    }

    public async Task BroadcastAuctionClosedAsync(Guid auctionId, Guid? winnerId, decimal finalPrice)
    {
        var groupName = $"auction:{auctionId}";

        await _hubContext.Clients.Group(groupName).SendAsync("AuctionClosed", new
        {
            AuctionId = auctionId,
            WinnerId = winnerId,
            FinalPrice = finalPrice
        });
    }

    public async Task NotifyUserAsync(Guid userId, string eventType, object payload)
    {
        await _hubContext.Clients.User(userId.ToString()).SendAsync(eventType, payload);
    }
}
```

```csharp
// Infrastructure/SignalR/GracefulShutdownService.cs
public class GracefulShutdownService : IHostedService
{
    private readonly IHubContext<AuctionHub> _hubContext;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<GracefulShutdownService> _logger;
    private readonly TimeSpan _drainTimeout = TimeSpan.FromSeconds(30);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initiating graceful shutdown, notifying clients...");

        // Notify all clients to reconnect to a different instance
        await _hubContext.Clients.All.SendAsync(
            "ServerShuttingDown",
            new { ReconnectDelay = 1000 },
            cancellationToken);

        // Wait for connections to drain
        _logger.LogInformation("Waiting {Timeout}s for connections to drain...", _drainTimeout.TotalSeconds);
        await Task.Delay(_drainTimeout, cancellationToken);
    }
}
```

### 7. Rate Limiting Distribuido

**Desafio**: Rate limiting consistente atraves de multiplas instancias.

**Solucao**: Redis-based sliding window rate limiter.

```csharp
// Infrastructure/RateLimiting/RedisRateLimiter.cs
public interface IDistributedRateLimiter
{
    Task<RateLimitResult> CheckRateLimitAsync(
        string key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}

public record RateLimitResult(
    bool IsAllowed,
    int Remaining,
    TimeSpan RetryAfter);

public class RedisRateLimiter : IDistributedRateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimiter> _logger;

    // Lua script for atomic sliding window rate limiting
    private const string RateLimitScript = @"
        local key = KEYS[1]
        local now = tonumber(ARGV[1])
        local window = tonumber(ARGV[2])
        local limit = tonumber(ARGV[3])
        local clearBefore = now - window

        -- Remove old entries
        redis.call('ZREMRANGEBYSCORE', key, 0, clearBefore)

        -- Count current entries
        local count = redis.call('ZCARD', key)

        if count < limit then
            -- Add new entry
            redis.call('ZADD', key, now, now .. '-' .. math.random())
            redis.call('EXPIRE', key, window / 1000 + 1)
            return {1, limit - count - 1, 0}
        else
            -- Get oldest entry to calculate retry time
            local oldest = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
            local retryAfter = 0
            if #oldest > 0 then
                retryAfter = (tonumber(oldest[2]) + window) - now
            end
            return {0, 0, retryAfter}
        end
    ";

    public async Task<RateLimitResult> CheckRateLimitAsync(
        string key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowMs = (long)window.TotalMilliseconds;

        try
        {
            var result = (RedisResult[]?)await db.ScriptEvaluateAsync(
                RateLimitScript,
                new RedisKey[] { $"ratelimit:{key}" },
                new RedisValue[] { now, windowMs, limit });

            if (result is null)
                return new RateLimitResult(true, limit, TimeSpan.Zero);

            var isAllowed = (int)result[0] == 1;
            var remaining = (int)result[1];
            var retryAfterMs = (long)result[2];

            return new RateLimitResult(
                isAllowed,
                remaining,
                TimeSpan.FromMilliseconds(retryAfterMs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rate limit check failed for key {Key}, allowing request", key);
            // Fail open - allow request if Redis is down
            return new RateLimitResult(true, limit, TimeSpan.Zero);
        }
    }
}
```

```csharp
// API/Filters/DistributedRateLimitFilter.cs
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RateLimitAttribute : Attribute, IAsyncActionFilter
{
    public int Limit { get; set; } = 10;
    public int WindowSeconds { get; set; } = 60;
    public string? PolicyName { get; set; }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var rateLimiter = context.HttpContext.RequestServices
            .GetRequiredService<IDistributedRateLimiter>();

        var userId = context.HttpContext.User.FindFirst("sub")?.Value
            ?? context.HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        var endpoint = context.ActionDescriptor.DisplayName;
        var key = $"{PolicyName ?? endpoint}:{userId}";

        var result = await rateLimiter.CheckRateLimitAsync(
            key,
            Limit,
            TimeSpan.FromSeconds(WindowSeconds));

        // Add rate limit headers
        context.HttpContext.Response.Headers["X-RateLimit-Limit"] = Limit.ToString();
        context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();

        if (!result.IsAllowed)
        {
            context.HttpContext.Response.Headers["Retry-After"] =
                ((int)result.RetryAfter.TotalSeconds).ToString();

            context.Result = new StatusCodeResult(StatusCodes.Status429TooManyRequests);
            return;
        }

        await next();
    }
}
```

```csharp
// Usage in Controller
[ApiController]
[Route("api/v{version:apiVersion}/auctions")]
[ApiVersion("1.0")]
public class AuctionsController : ControllerBase
{
    [HttpPost("{auctionId}/bids")]
    [RateLimit(Limit = 10, WindowSeconds = 60, PolicyName = "PlaceBid")]
    public async Task<IActionResult> PlaceBid(
        Guid auctionId,
        [FromBody] PlaceBidRequest request,
        CancellationToken cancellationToken)
    {
        // ...
    }
}
```

---

## Seguranca

### AWS Cognito - Configuracao Completa

```csharp
// Program.cs - Authentication Configuration
var cognitoConfig = builder.Configuration.GetSection("AWS:Cognito");
var region = cognitoConfig["Region"];
var userPoolId = cognitoConfig["UserPoolId"];
var appClientId = cognitoConfig["AppClientId"];
var authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = appClientId;
        options.RequireHttpsMetadata = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = authority,
            ValidAudience = appClientId,
            NameClaimType = "cognito:username",
            RoleClaimType = "cognito:groups"
        };

        options.Events = new JwtBearerEvents
        {
            // SignalR token handling - prefer header over query string
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                if (!path.StartsWithSegments("/hubs"))
                    return Task.CompletedTask;

                // Priority: Header > Cookie > Query String
                var token = context.Request.Headers["X-SignalR-Token"].FirstOrDefault()
                    ?? context.Request.Cookies["signalr_token"];

                // Query string as fallback (logged as warning)
                if (string.IsNullOrEmpty(token))
                {
                    token = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token))
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();
                        logger.LogWarning(
                            "Token passed via query string for connection {ConnectionId}",
                            context.HttpContext.Connection.Id);
                    }
                }

                if (!string.IsNullOrEmpty(token))
                    context.Token = token;

                return Task.CompletedTask;
            },

            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.Headers.Append("X-Token-Expired", "true");
                }
                return Task.CompletedTask;
            },

            OnTokenValidated = async context =>
            {
                var tokenBlacklist = context.HttpContext.RequestServices
                    .GetRequiredService<ITokenBlacklistService>();

                var jti = context.Principal?.FindFirst("jti")?.Value;
                if (!string.IsNullOrEmpty(jti) && await tokenBlacklist.IsRevokedAsync(jti))
                {
                    context.Fail("Token has been revoked");
                    return;
                }

                // Enrich claims with user profile data
                var userService = context.HttpContext.RequestServices
                    .GetRequiredService<IUserService>();

                var userId = context.Principal?.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var profile = await userService.GetUserProfileAsync(userId);
                    if (profile is not null)
                    {
                        var identity = context.Principal!.Identity as ClaimsIdentity;
                        identity?.AddClaims(new[]
                        {
                            new Claim("reputation", profile.ReputationScore.ToString()),
                            new Claim("is_verified", profile.IsVerified.ToString()),
                            new Claim("total_bids", profile.TotalBids.ToString())
                        });
                    }
                }
            }
        };
    });
```

### Token Revocation (Blacklist)

```csharp
// Infrastructure/Security/TokenBlacklistService.cs
public interface ITokenBlacklistService
{
    Task RevokeTokenAsync(string jti, TimeSpan remainingLifetime);
    Task<bool> IsRevokedAsync(string jti);
    Task RevokeAllUserTokensAsync(Guid userId);
}

public class RedisTokenBlacklistService : ITokenBlacklistService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisTokenBlacklistService> _logger;
    private const string KeyPrefix = "token:revoked:";
    private const string UserTokensPrefix = "user:tokens:";

    public async Task RevokeTokenAsync(string jti, TimeSpan remainingLifetime)
    {
        if (remainingLifetime <= TimeSpan.Zero)
            return; // Token already expired

        await _cache.SetStringAsync(
            $"{KeyPrefix}{jti}",
            "revoked",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = remainingLifetime
            });

        _logger.LogInformation("Token {Jti} revoked, expires in {Lifetime}", jti, remainingLifetime);
    }

    public async Task<bool> IsRevokedAsync(string jti)
    {
        var result = await _cache.GetStringAsync($"{KeyPrefix}{jti}");
        return result is not null;
    }

    public async Task RevokeAllUserTokensAsync(Guid userId)
    {
        // Store a "revoke all before" timestamp for the user
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _cache.SetStringAsync(
            $"{UserTokensPrefix}{userId}:revoke_before",
            timestamp.ToString(),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
            });

        _logger.LogInformation("All tokens revoked for user {UserId}", userId);
    }
}
```

### Authorization Policies

```csharp
// Program.cs - Authorization Configuration
builder.Services.AddAuthorization(options =>
{
    // Basic policies
    options.AddPolicy("VerifiedEmail", policy =>
        policy.RequireClaim("email_verified", "true"));

    options.AddPolicy("Seller", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Sellers") ||
            context.User.IsInRole("Admins")));

    options.AddPolicy("Admin", policy =>
        policy.RequireRole("Admins"));

    // Complex policies with requirements
    options.AddPolicy("CanBid", policy =>
        policy.AddRequirements(new CanBidRequirement()));

    options.AddPolicy("CanCreateAuction", policy =>
        policy.AddRequirements(new CanCreateAuctionRequirement()));

    options.AddPolicy("HighValueBid", policy =>
        policy.AddRequirements(new HighValueBidRequirement(threshold: 10000m)));
});

builder.Services.AddScoped<IAuthorizationHandler, CanBidAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, CanCreateAuctionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, HighValueBidAuthorizationHandler>();
```

```csharp
// Infrastructure/Authorization/CanBidAuthorizationHandler.cs
public class CanBidRequirement : IAuthorizationRequirement
{
    public decimal MinimumReputation { get; } = 0.5m;
}

public class CanBidAuthorizationHandler : AuthorizationHandler<CanBidRequirement>
{
    private readonly IUserService _userService;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CanBidRequirement requirement)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            context.Fail(new AuthorizationFailureReason(this, "User not authenticated"));
            return;
        }

        // Check email verification
        var emailVerified = context.User.FindFirst("email_verified")?.Value;
        if (emailVerified != "true")
        {
            context.Fail(new AuthorizationFailureReason(this, "Email not verified"));
            return;
        }

        // Check reputation from claims or fetch from DB
        var reputationClaim = context.User.FindFirst("reputation")?.Value;
        decimal reputation;

        if (!string.IsNullOrEmpty(reputationClaim) &&
            decimal.TryParse(reputationClaim, out reputation))
        {
            if (reputation >= requirement.MinimumReputation)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // Fallback to database check
        var profile = await _userService.GetUserProfileAsync(userId);
        if (profile?.ReputationScore >= requirement.MinimumReputation)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail(new AuthorizationFailureReason(
            this, $"Reputation score below minimum ({requirement.MinimumReputation})"));
    }
}
```

```csharp
// Infrastructure/Authorization/HighValueBidAuthorizationHandler.cs
public class HighValueBidRequirement : IAuthorizationRequirement
{
    public decimal Threshold { get; }

    public HighValueBidRequirement(decimal threshold) => Threshold = threshold;
}

public class HighValueBidAuthorizationHandler
    : AuthorizationHandler<HighValueBidRequirement, PlaceBidCommand>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HighValueBidRequirement requirement,
        PlaceBidCommand resource)
    {
        // Only enforce for bids above threshold
        if (resource.Amount < requirement.Threshold)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Require MFA for high-value bids
        var amr = context.User.FindFirst("amr")?.Value;
        if (amr?.Contains("mfa") == true)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if KYC verified
        var kycVerified = context.User.FindFirst("custom:kyc_verified")?.Value;
        if (kycVerified == "true")
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        context.Fail(new AuthorizationFailureReason(
            this, "MFA or KYC verification required for high-value bids"));

        return Task.CompletedTask;
    }
}
```

### Secrets Management

```csharp
// Program.cs - AWS Secrets Manager Integration
builder.Configuration.AddSecretsManager(configurator: options =>
{
    options.SecretFilter = entry => entry.Name.StartsWith("AuctionKings/");
    options.KeyGenerator = (_, name) => name.Replace("AuctionKings/", "").Replace("/", ":");
    options.PollingInterval = TimeSpan.FromMinutes(5);
});

// Alternative: Using IOptions pattern
builder.Services.Configure<DatabaseSecrets>(
    builder.Configuration.GetSection("Database"));
builder.Services.Configure<CognitoSecrets>(
    builder.Configuration.GetSection("Cognito"));
```

```csharp
// Infrastructure/Configuration/SecretsRotationService.cs
public class SecretsRotationService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SecretsRotationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Reload configuration from Secrets Manager
                if (_configuration is IConfigurationRoot configRoot)
                {
                    configRoot.Reload();
                    _logger.LogDebug("Configuration reloaded from Secrets Manager");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload secrets");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
```

---

## Estrutura de Pastas (Atualizada)

```
AuctionKings/
├── src/
│   ├── AuctionKings.API/
│   │   ├── Controllers/
│   │   │   ├── V1/
│   │   │   │   ├── AuctionsController.cs
│   │   │   │   ├── BidsController.cs
│   │   │   │   └── UsersController.cs
│   │   │   └── V2/                           # Future API versions
│   │   ├── Hubs/
│   │   │   ├── AuctionHub.cs
│   │   │   └── NotificationHub.cs
│   │   ├── Filters/
│   │   │   ├── RateLimitAttribute.cs
│   │   │   ├── IdempotencyFilter.cs
│   │   │   └── GlobalExceptionFilter.cs
│   │   ├── Middleware/
│   │   │   ├── CorrelationIdMiddleware.cs
│   │   │   ├── RequestLoggingMiddleware.cs
│   │   │   └── SecurityHeadersMiddleware.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.{Environment}.json
│   │
│   ├── AuctionKings.Application/
│   │   ├── Commands/
│   │   │   ├── Auctions/
│   │   │   │   ├── CreateAuctionCommand.cs
│   │   │   │   ├── CloseAuctionCommand.cs
│   │   │   │   └── Handlers/
│   │   │   │       ├── CreateAuctionCommandHandler.cs
│   │   │   │       └── CloseAuctionCommandHandler.cs
│   │   │   └── Bids/
│   │   │       ├── PlaceBidCommand.cs
│   │   │       └── Handlers/
│   │   │           └── PlaceBidCommandHandler.cs
│   │   ├── Queries/
│   │   │   ├── Auctions/
│   │   │   │   ├── GetAuctionDetailsQuery.cs
│   │   │   │   ├── GetActiveAuctionsQuery.cs
│   │   │   │   └── Handlers/
│   │   │   └── Bids/
│   │   │       ├── GetBidHistoryQuery.cs
│   │   │       └── Handlers/
│   │   ├── DTOs/
│   │   │   ├── AuctionDto.cs
│   │   │   ├── AuctionStateDto.cs
│   │   │   ├── BidDto.cs
│   │   │   └── UserDto.cs
│   │   ├── Behaviors/                        # MediatR Pipeline Behaviors
│   │   │   ├── ValidationBehavior.cs
│   │   │   ├── LoggingBehavior.cs
│   │   │   ├── IdempotencyBehavior.cs
│   │   │   └── PerformanceBehavior.cs
│   │   ├── Interfaces/
│   │   │   ├── IAuctionRepository.cs
│   │   │   ├── IEventStore.cs
│   │   │   ├── IUnitOfWork.cs
│   │   │   └── IRealtimeNotificationService.cs
│   │   ├── Sagas/
│   │   │   ├── AuctionClosingSagaHandler.cs
│   │   │   ├── AuctionClosingSagaState.cs
│   │   │   └── ISagaStateRepository.cs
│   │   └── Validators/
│   │       ├── PlaceBidCommandValidator.cs
│   │       └── CreateAuctionCommandValidator.cs
│   │
│   ├── AuctionKings.Domain/
│   │   ├── Aggregates/
│   │   │   ├── Auction/
│   │   │   │   ├── Auction.cs                # Aggregate Root
│   │   │   │   ├── AuctionSnapshot.cs
│   │   │   │   ├── Bid.cs                    # Entity
│   │   │   │   └── AuctionRules.cs           # Domain Service
│   │   │   └── User/
│   │   │       ├── User.cs
│   │   │       └── UserProfile.cs
│   │   ├── Events/
│   │   │   ├── AuctionCreatedEvent.cs
│   │   │   ├── BidPlacedEvent.cs
│   │   │   ├── AuctionExtendedEvent.cs
│   │   │   ├── AuctionClosedEvent.cs
│   │   │   └── WinnerDeterminedEvent.cs
│   │   ├── ValueObjects/
│   │   │   ├── Money.cs
│   │   │   ├── AuctionDuration.cs
│   │   │   └── BidIncrement.cs
│   │   ├── Enums/
│   │   │   ├── AuctionStatus.cs
│   │   │   └── BidStatus.cs
│   │   ├── Errors/
│   │   │   ├── DomainErrors.cs
│   │   │   └── Error.cs
│   │   ├── Common/
│   │   │   ├── AggregateRoot.cs
│   │   │   ├── Entity.cs
│   │   │   ├── ValueObject.cs
│   │   │   ├── IDomainEvent.cs
│   │   │   └── DomainEventTypeRegistry.cs
│   │   └── Specifications/
│   │       ├── ActiveAuctionSpecification.cs
│   │       └── BiddableAuctionSpecification.cs
│   │
│   ├── AuctionKings.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── AuctionDbContext.cs
│   │   │   ├── UnitOfWork.cs
│   │   │   ├── OutboxMessage.cs
│   │   │   ├── Configurations/
│   │   │   │   ├── AuctionConfiguration.cs
│   │   │   │   ├── BidConfiguration.cs
│   │   │   │   ├── OutboxMessageConfiguration.cs
│   │   │   │   └── SagaStateConfiguration.cs
│   │   │   ├── Repositories/
│   │   │   │   ├── AuctionRepository.cs
│   │   │   │   ├── BidRepository.cs
│   │   │   │   └── SagaStateRepository.cs
│   │   │   └── Migrations/
│   │   ├── EventStore/
│   │   │   ├── DynamoDbEventStore.cs
│   │   │   ├── EventSourcedAuctionRepository.cs
│   │   │   └── ConcurrencyException.cs
│   │   ├── Caching/
│   │   │   ├── RedisCacheService.cs
│   │   │   ├── CacheKeys.cs
│   │   │   └── CacheInvalidationService.cs
│   │   ├── Messaging/
│   │   │   ├── SqsMessagePublisher.cs
│   │   │   ├── SnsNotificationService.cs
│   │   │   └── IEventPublisher.cs
│   │   ├── RateLimiting/
│   │   │   └── RedisRateLimiter.cs
│   │   ├── SignalR/
│   │   │   ├── SignalRNotificationService.cs
│   │   │   ├── ConnectionTracker.cs
│   │   │   └── GracefulShutdownService.cs
│   │   ├── Security/
│   │   │   ├── TokenBlacklistService.cs
│   │   │   └── CognitoService.cs
│   │   ├── Authorization/
│   │   │   ├── CanBidAuthorizationHandler.cs
│   │   │   ├── CanCreateAuctionAuthorizationHandler.cs
│   │   │   └── HighValueBidAuthorizationHandler.cs
│   │   ├── BackgroundServices/
│   │   │   └── OutboxProcessor.cs
│   │   ├── ExternalServices/
│   │   │   ├── PaymentGateway/
│   │   │   │   └── StripePaymentService.cs
│   │   │   ├── Storage/
│   │   │   │   └── S3StorageService.cs
│   │   │   └── Email/
│   │   │       └── SesEmailService.cs
│   │   └── Monitoring/
│   │       ├── PrometheusMetrics.cs
│   │       ├── HealthChecks/
│   │       │   ├── DatabaseHealthCheck.cs
│   │       │   ├── RedisHealthCheck.cs
│   │       │   └── SignalRHealthCheck.cs
│   │       └── XRayTracing.cs
│   │
│   ├── AuctionKings.BackgroundWorkers/
│   │   ├── Workers/
│   │   │   ├── AuctionCloseWorker.cs
│   │   │   ├── BidProcessorWorker.cs
│   │   │   ├── NotificationWorker.cs
│   │   │   ├── ProjectionWorker.cs
│   │   │   └── DlqProcessorWorker.cs         # Dead Letter Queue processor
│   │   └── Jobs/
│   │       ├── CleanupExpiredDataJob.cs
│   │       ├── SnapshotCreationJob.cs
│   │       └── ReportGenerationJob.cs
│   │
│   └── AuctionKings.Shared/
│       ├── Common/
│       │   ├── Result.cs
│       │   ├── PagedResult.cs
│       │   └── Constants.cs
│       ├── Extensions/
│       │   ├── DateTimeExtensions.cs
│       │   ├── StringExtensions.cs
│       │   └── ClaimsPrincipalExtensions.cs
│       └── Interfaces/
│           ├── IDateTimeProvider.cs
│           └── IIdempotentCommand.cs
│
├── tests/
│   ├── AuctionKings.UnitTests/
│   │   ├── Domain/
│   │   │   ├── AuctionTests.cs
│   │   │   ├── BidTests.cs
│   │   │   └── ValueObjectTests.cs
│   │   ├── Application/
│   │   │   ├── PlaceBidCommandHandlerTests.cs
│   │   │   └── IdempotencyBehaviorTests.cs
│   │   └── Infrastructure/
│   │       └── RedisRateLimiterTests.cs
│   ├── AuctionKings.IntegrationTests/
│   │   ├── API/
│   │   │   ├── AuctionsControllerTests.cs
│   │   │   └── BidsControllerTests.cs
│   │   ├── Infrastructure/
│   │   │   ├── RepositoryTests.cs
│   │   │   └── OutboxProcessorTests.cs
│   │   ├── Fixtures/
│   │   │   ├── DatabaseFixture.cs
│   │   │   └── RedisFixture.cs
│   │   └── TestContainers/
│   │       └── PostgresContainerFixture.cs
│   ├── AuctionKings.ArchitectureTests/
│   │   ├── DependencyTests.cs
│   │   └── NamingConventionTests.cs
│   └── AuctionKings.LoadTests/
│       ├── BidStormTest.cs
│       ├── SignalRLoadTest.cs
│       └── k6/
│           └── auction-load-test.js
│
├── docker/
│   ├── Dockerfile.api
│   ├── Dockerfile.worker
│   ├── docker-compose.yml
│   └── docker-compose.dev.yml
│
├── deploy/
│   ├── terraform/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   ├── outputs.tf
│   │   ├── modules/
│   │   │   ├── vpc/
│   │   │   ├── ecs/
│   │   │   ├── rds/
│   │   │   ├── elasticache/
│   │   │   ├── dynamodb/
│   │   │   └── cognito/
│   │   └── environments/
│   │       ├── dev/
│   │       ├── staging/
│   │       └── prod/
│   └── github-actions/
│       ├── ci.yml
│       ├── cd-staging.yml
│       └── cd-production.yml
│
├── monitoring/
│   ├── prometheus/
│   │   ├── prometheus.yml
│   │   └── alerts/
│   │       └── auction-alerts.yml
│   └── grafana/
│       └── dashboards/
│           ├── auction-overview.json
│           ├── signalr-metrics.json
│           └── system-health.json
│
├── docs/
│   ├── adr/                                   # Architecture Decision Records
│   │   ├── 001-use-event-sourcing.md
│   │   ├── 002-outbox-pattern.md
│   │   └── 003-distributed-rate-limiting.md
│   └── runbooks/
│       ├── incident-response.md
│       └── deployment-rollback.md
│
├── scripts/
│   ├── setup-local-env.sh
│   ├── run-migrations.sh
│   └── smoke-tests.sh
│
├── AuctionKings.sln
├── .editorconfig
├── .gitignore
├── Directory.Build.props
├── Directory.Packages.props                   # Central package management
└── global.json
```

---

## Modelo de Dados (Corrigido)

### PostgreSQL Schema

```sql
-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Users Table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    cognito_sub VARCHAR(255) UNIQUE NOT NULL,
    email VARCHAR(255) UNIQUE NOT NULL,
    username VARCHAR(50) UNIQUE NOT NULL,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    phone VARCHAR(20),
    is_verified BOOLEAN DEFAULT false,
    is_seller BOOLEAN DEFAULT false,
    reputation_score DECIMAL(5,2) DEFAULT 1.00,
    total_bids INT DEFAULT 0,
    total_wins INT DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    last_login_at TIMESTAMPTZ,

    CONSTRAINT valid_reputation CHECK (reputation_score >= 0 AND reputation_score <= 5)
);

CREATE INDEX idx_users_cognito_sub ON users(cognito_sub);
CREATE INDEX idx_users_email ON users(email);

-- Auctions Table
CREATE TABLE auctions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    seller_id UUID NOT NULL REFERENCES users(id),
    title VARCHAR(200) NOT NULL,
    description TEXT,
    starting_price DECIMAL(18,2) NOT NULL,
    current_price DECIMAL(18,2) NOT NULL,
    reserve_price DECIMAL(18,2),
    buy_now_price DECIMAL(18,2),
    bid_increment DECIMAL(18,2) NOT NULL DEFAULT 1.00,
    start_time TIMESTAMPTZ NOT NULL,
    end_time TIMESTAMPTZ NOT NULL,
    original_end_time TIMESTAMPTZ NOT NULL,
    auto_extend_enabled BOOLEAN DEFAULT true,
    auto_extend_minutes INT DEFAULT 2,
    status VARCHAR(20) NOT NULL DEFAULT 'Draft',
    current_winner_id UUID REFERENCES users(id),
    total_bids INT DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    -- xmin is used automatically by PostgreSQL for optimistic concurrency

    CONSTRAINT valid_price_range CHECK (current_price >= starting_price),
    CONSTRAINT valid_time_range CHECK (end_time > start_time),
    CONSTRAINT valid_status CHECK (status IN ('Draft', 'Active', 'Closing', 'Closed', 'Cancelled')),
    CONSTRAINT valid_reserve CHECK (reserve_price IS NULL OR reserve_price >= starting_price)
);

-- Indexes for common query patterns
CREATE INDEX idx_auctions_status ON auctions(status);
CREATE INDEX idx_auctions_seller ON auctions(seller_id);
CREATE INDEX idx_auctions_current_winner ON auctions(current_winner_id) WHERE current_winner_id IS NOT NULL;

-- Partial index for active auctions ending soon (used by closing worker)
CREATE INDEX idx_auctions_active_ending ON auctions(end_time)
    WHERE status = 'Active';

-- Composite index for listing queries
CREATE INDEX idx_auctions_status_end_time ON auctions(status, end_time DESC);

-- Bids Table (removed problematic unique constraint)
CREATE TABLE bids (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    auction_id UUID NOT NULL REFERENCES auctions(id),
    user_id UUID NOT NULL REFERENCES users(id),
    amount DECIMAL(18,2) NOT NULL,
    request_id UUID NOT NULL,  -- Idempotency key
    is_auto_bid BOOLEAN DEFAULT false,
    status VARCHAR(20) NOT NULL DEFAULT 'Active',
    ip_address INET,
    user_agent TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT valid_bid_status CHECK (status IN ('Active', 'Outbid', 'Winning', 'Won', 'Lost', 'Cancelled')),
    CONSTRAINT unique_request_id UNIQUE(request_id)  -- Idempotency at DB level
);

-- Indexes for bids
CREATE INDEX idx_bids_auction ON bids(auction_id, created_at DESC);
CREATE INDEX idx_bids_auction_amount ON bids(auction_id, amount DESC);
CREATE INDEX idx_bids_user ON bids(user_id, created_at DESC);
CREATE INDEX idx_bids_status ON bids(auction_id, status) WHERE status = 'Winning';

-- Outbox Table (for reliable event publishing)
CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    type VARCHAR(500) NOT NULL,
    payload JSONB NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    processed_at TIMESTAMPTZ,
    error TEXT,
    retry_count INT DEFAULT 0
);

-- Index for outbox processor
CREATE INDEX idx_outbox_unprocessed ON outbox_messages(created_at)
    WHERE processed_at IS NULL AND retry_count < 5;

-- Saga State Table
CREATE TABLE saga_states (
    saga_id UUID PRIMARY KEY,
    saga_type VARCHAR(200) NOT NULL,
    auction_id UUID NOT NULL,
    status VARCHAR(50) NOT NULL,
    current_step INT DEFAULT 0,
    state_data JSONB NOT NULL,
    error_message TEXT,
    started_at TIMESTAMPTZ DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    last_updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_saga_auction ON saga_states(auction_id);
CREATE INDEX idx_saga_status ON saga_states(status) WHERE status NOT IN ('Completed', 'Failed');

-- Idempotency Keys Table (backup for cache misses)
CREATE TABLE idempotency_keys (
    key VARCHAR(500) PRIMARY KEY,
    response JSONB NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX idx_idempotency_expires ON idempotency_keys(expires_at);

-- Notifications Table
CREATE TABLE notifications (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id),
    type VARCHAR(50) NOT NULL,
    title VARCHAR(200) NOT NULL,
    message TEXT,
    is_read BOOLEAN DEFAULT false,
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_notifications_user_unread ON notifications(user_id, created_at DESC)
    WHERE is_read = false;

-- Function to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Trigger for auctions
CREATE TRIGGER update_auctions_updated_at
    BEFORE UPDATE ON auctions
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Cleanup job for expired idempotency keys (run via pg_cron or external scheduler)
-- DELETE FROM idempotency_keys WHERE expires_at < NOW();
```

### DynamoDB Event Store Schema

```json
{
  "TableName": "AuctionEvents",
  "KeySchema": [
    { "AttributeName": "StreamId", "KeyType": "HASH" },
    { "AttributeName": "Version", "KeyType": "RANGE" }
  ],
  "AttributeDefinitions": [
    { "AttributeName": "StreamId", "AttributeType": "S" },
    { "AttributeName": "Version", "AttributeType": "N" },
    { "AttributeName": "EventType", "AttributeType": "S" },
    { "AttributeName": "Timestamp", "AttributeType": "N" }
  ],
  "GlobalSecondaryIndexes": [
    {
      "IndexName": "EventTypeTimestampIndex",
      "KeySchema": [
        { "AttributeName": "EventType", "KeyType": "HASH" },
        { "AttributeName": "Timestamp", "KeyType": "RANGE" }
      ],
      "Projection": { "ProjectionType": "ALL" }
    }
  ],
  "BillingMode": "PAY_PER_REQUEST",
  "StreamSpecification": {
    "StreamEnabled": true,
    "StreamViewType": "NEW_IMAGE"
  },
  "PointInTimeRecoverySpecification": {
    "PointInTimeRecoveryEnabled": true
  }
}
```

```json
{
  "TableName": "AuctionSnapshots",
  "KeySchema": [
    { "AttributeName": "StreamId", "KeyType": "HASH" },
    { "AttributeName": "Version", "KeyType": "RANGE" }
  ],
  "AttributeDefinitions": [
    { "AttributeName": "StreamId", "AttributeType": "S" },
    { "AttributeName": "Version", "AttributeType": "N" }
  ],
  "BillingMode": "PAY_PER_REQUEST",
  "TimeToLiveSpecification": {
    "AttributeName": "ExpiresAt",
    "Enabled": true
  }
}
```

---

## Health Checks

```csharp
// Program.cs - Health Checks Configuration
builder.Services.AddHealthChecks()
    // Database
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("PostgreSQL")!,
        name: "postgresql",
        tags: new[] { "ready", "db" })

    // Redis
    .AddRedis(
        redisConnectionString: builder.Configuration.GetConnectionString("Redis")!,
        name: "redis",
        tags: new[] { "ready", "cache" })

    // DynamoDB
    .AddDynamoDb(options =>
    {
        options.RegionEndpoint = RegionEndpoint.GetBySystemName(
            builder.Configuration["AWS:Region"]);
    }, name: "dynamodb", tags: new[] { "ready", "eventstore" })

    // SQS
    .AddSqs(options =>
    {
        options.RegionEndpoint = RegionEndpoint.GetBySystemName(
            builder.Configuration["AWS:Region"]);
        options.QueueUrls = new[] { builder.Configuration["AWS:SQS:BidQueueUrl"]! };
    }, name: "sqs", tags: new[] { "ready", "messaging" })

    // Custom SignalR health check
    .AddCheck<SignalRHealthCheck>("signalr", tags: new[] { "ready", "realtime" })

    // Custom outbox processor check
    .AddCheck<OutboxHealthCheck>("outbox", tags: new[] { "ready", "messaging" });

// Map health endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // No checks - just verifies app is running
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/db", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

```csharp
// Infrastructure/Monitoring/HealthChecks/SignalRHealthCheck.cs
public class SignalRHealthCheck : IHealthCheck
{
    private readonly IConnectionTracker _connectionTracker;
    private readonly ILogger<SignalRHealthCheck> _logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionCount = await _connectionTracker.GetTotalConnectionCountAsync();

            var data = new Dictionary<string, object>
            {
                { "connections", connectionCount }
            };

            // Warning if no connections but auctions are active
            if (connectionCount == 0)
            {
                return HealthCheckResult.Healthy(
                    "SignalR operational, no active connections",
                    data);
            }

            return HealthCheckResult.Healthy(
                $"SignalR operational with {connectionCount} connections",
                data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR health check failed");
            return HealthCheckResult.Unhealthy("SignalR health check failed", ex);
        }
    }
}
```

```csharp
// Infrastructure/Monitoring/HealthChecks/OutboxHealthCheck.cs
public class OutboxHealthCheck : IHealthCheck
{
    private readonly AuctionDbContext _context;
    private readonly ILogger<OutboxHealthCheck> _logger;
    private const int MaxUnprocessedAge = 60; // seconds

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var threshold = DateTime.UtcNow.AddSeconds(-MaxUnprocessedAge);

            var oldestUnprocessed = await _context.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
                .OrderBy(m => m.CreatedAt)
                .Select(m => m.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var unprocessedCount = await _context.OutboxMessages
                .CountAsync(m => m.ProcessedAt == null, cancellationToken);

            var failedCount = await _context.OutboxMessages
                .CountAsync(m => m.RetryCount >= 5, cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "unprocessed_count", unprocessedCount },
                { "failed_count", failedCount }
            };

            if (failedCount > 0)
            {
                return HealthCheckResult.Degraded(
                    $"Outbox has {failedCount} failed messages",
                    data: data);
            }

            if (oldestUnprocessed != default && oldestUnprocessed < threshold)
            {
                return HealthCheckResult.Degraded(
                    $"Outbox has messages older than {MaxUnprocessedAge}s",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Outbox healthy with {unprocessedCount} pending messages",
                data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox health check failed");
            return HealthCheckResult.Unhealthy("Outbox health check failed", ex);
        }
    }
}
```

---

## Observabilidade

### Metricas Prometheus

```csharp
// Infrastructure/Monitoring/AuctionMetrics.cs
public static class AuctionMetrics
{
    private static readonly Counter BidsPlaced = Metrics.CreateCounter(
        "auction_bids_placed_total",
        "Total number of bids placed",
        new CounterConfiguration
        {
            LabelNames = new[] { "auction_id", "status" }
        });

    private static readonly Counter BidsConcurrencyConflicts = Metrics.CreateCounter(
        "auction_bids_concurrency_conflicts_total",
        "Number of bid concurrency conflicts");

    private static readonly Histogram BidProcessingDuration = Metrics.CreateHistogram(
        "auction_bid_processing_duration_seconds",
        "Time taken to process a bid",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 12) // 1ms to ~4s
        });

    private static readonly Gauge ActiveAuctions = Metrics.CreateGauge(
        "auction_active_count",
        "Number of currently active auctions");

    private static readonly Gauge SignalRConnections = Metrics.CreateGauge(
        "auction_signalr_connections_total",
        "Total number of SignalR connections");

    private static readonly Counter OutboxMessagesProcessed = Metrics.CreateCounter(
        "auction_outbox_messages_processed_total",
        "Number of outbox messages processed",
        new CounterConfiguration
        {
            LabelNames = new[] { "status" } // success, failed
        });

    private static readonly Gauge OutboxMessagesPending = Metrics.CreateGauge(
        "auction_outbox_messages_pending",
        "Number of pending outbox messages");

    private static readonly Histogram EventStoreLatency = Metrics.CreateHistogram(
        "auction_eventstore_operation_duration_seconds",
        "Event store operation duration",
        new HistogramConfiguration
        {
            LabelNames = new[] { "operation" }, // append, read, snapshot
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
        });

    public static void RecordBidPlaced(string auctionId, string status) =>
        BidsPlaced.WithLabels(auctionId, status).Inc();

    public static void RecordConcurrencyConflict() =>
        BidsConcurrencyConflicts.Inc();

    public static IDisposable MeasureBidProcessing() =>
        BidProcessingDuration.NewTimer();

    public static void SetActiveAuctions(int count) =>
        ActiveAuctions.Set(count);

    public static void SetSignalRConnections(int count) =>
        SignalRConnections.Set(count);

    public static void RecordOutboxProcessed(bool success) =>
        OutboxMessagesProcessed.WithLabels(success ? "success" : "failed").Inc();

    public static void SetOutboxPending(int count) =>
        OutboxMessagesPending.Set(count);

    public static IDisposable MeasureEventStoreOperation(string operation) =>
        EventStoreLatency.WithLabels(operation).NewTimer();
}
```

### Alerting Rules (Prometheus)

```yaml
# monitoring/prometheus/alerts/auction-alerts.yml
groups:
  - name: auction_critical
    rules:
      - alert: HighBidLatency
        expr: histogram_quantile(0.95, rate(auction_bid_processing_duration_seconds_bucket[5m])) > 1.0
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Bid processing latency is too high"
          description: "95th percentile bid latency is {{ $value | humanizeDuration }} (threshold: 1s)"

      - alert: OutboxBacklogGrowing
        expr: auction_outbox_messages_pending > 1000
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Outbox message backlog is growing"
          description: "{{ $value }} messages pending in outbox"

      - alert: SignalRConnectionsDrop
        expr: |
          (auction_signalr_connections_total - auction_signalr_connections_total offset 5m)
          / auction_signalr_connections_total offset 5m < -0.5
          and auction_signalr_connections_total offset 5m > 100
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "SignalR connections dropped by more than 50%"

      - alert: HighConcurrencyConflicts
        expr: rate(auction_bids_concurrency_conflicts_total[5m]) > 10
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High rate of bid concurrency conflicts"
          description: "{{ $value | humanize }} conflicts per second"

  - name: auction_warnings
    rules:
      - alert: NoActiveBids
        expr: |
          rate(auction_bids_placed_total[10m]) == 0
          and auction_active_count > 0
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "No bids placed in the last 10 minutes"
          description: "Active auctions: {{ $value }}"

      - alert: EventStoreLatencyHigh
        expr: histogram_quantile(0.95, rate(auction_eventstore_operation_duration_seconds_bucket[5m])) > 0.5
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Event store latency is elevated"
```

---

## Tratamento de Erros

### Padrao Result

```csharp
// Shared/Common/Result.cs
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None ||
            !isSuccess && error == Error.None)
        {
            throw new ArgumentException("Invalid error state", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default!, false, error);
}

public class Result<T> : Result
{
    private readonly T _value;

    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("Cannot access value of failed result");

    protected internal Result(T value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public static implicit operator Result<T>(T value) =>
        value is not null ? Success(value) : Failure<T>(Error.NullValue);
}
```

```csharp
// Domain/Errors/Error.cs
public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "Value cannot be null");

    public static Error Validation(string description) =>
        new("Error.Validation", description);

    public static Error NotFound(string entity, object id) =>
        new($"{entity}.NotFound", $"{entity} with id '{id}' was not found");

    public static Error Conflict(string description) =>
        new("Error.Conflict", description);
}
```

```csharp
// Domain/Errors/DomainErrors.cs
public static class DomainErrors
{
    public static class Auction
    {
        public static readonly Error NotFound = new(
            "Auction.NotFound",
            "The auction was not found");

        public static readonly Error NotActive = new(
            "Auction.NotActive",
            "The auction is not active");

        public static readonly Error Ended = new(
            "Auction.Ended",
            "The auction has ended");

        public static readonly Error AlreadyClosed = new(
            "Auction.AlreadyClosed",
            "The auction is already closed");
    }

    public static class Bid
    {
        public static Error AmountTooLow(decimal currentPrice) => new(
            "Bid.AmountTooLow",
            $"Bid amount must be higher than current price ({currentPrice:C})");

        public static readonly Error AlreadyWinning = new(
            "Bid.AlreadyWinning",
            "You are already the highest bidder");

        public static readonly Error ConcurrentModification = new(
            "Bid.ConcurrentModification",
            "Another bid was placed. Please try again");

        public static readonly Error RateLimitExceeded = new(
            "Bid.RateLimitExceeded",
            "Too many bids. Please wait before trying again");
    }

    public static class User
    {
        public static readonly Error NotVerified = new(
            "User.NotVerified",
            "Email verification required");

        public static readonly Error InsufficientReputation = new(
            "User.InsufficientReputation",
            "Reputation score too low to perform this action");
    }
}
```

### Global Exception Handler

```csharp
// API/Filters/GlobalExceptionFilter.cs
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;
    private readonly IHostEnvironment _environment;

    public void OnException(ExceptionContext context)
    {
        var correlationId = context.HttpContext.TraceIdentifier;

        var (statusCode, problemDetails) = context.Exception switch
        {
            ValidationException ex => (
                StatusCodes.Status400BadRequest,
                CreateProblemDetails(
                    "Validation Error",
                    "One or more validation errors occurred",
                    StatusCodes.Status400BadRequest,
                    correlationId,
                    ex.Errors.ToDictionary(
                        e => e.PropertyName,
                        e => new[] { e.ErrorMessage }))),

            NotFoundException ex => (
                StatusCodes.Status404NotFound,
                CreateProblemDetails(
                    "Not Found",
                    ex.Message,
                    StatusCodes.Status404NotFound,
                    correlationId)),

            ConcurrencyException ex => (
                StatusCodes.Status409Conflict,
                CreateProblemDetails(
                    "Conflict",
                    ex.Message,
                    StatusCodes.Status409Conflict,
                    correlationId)),

            UnauthorizedAccessException ex => (
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(
                    "Forbidden",
                    ex.Message,
                    StatusCodes.Status403Forbidden,
                    correlationId)),

            _ => (
                StatusCodes.Status500InternalServerError,
                CreateProblemDetails(
                    "Internal Server Error",
                    _environment.IsDevelopment()
                        ? context.Exception.Message
                        : "An unexpected error occurred",
                    StatusCodes.Status500InternalServerError,
                    correlationId))
        };

        if (statusCode >= 500)
        {
            _logger.LogError(
                context.Exception,
                "Unhandled exception for request {CorrelationId}",
                correlationId);
        }
        else
        {
            _logger.LogWarning(
                "Request {CorrelationId} failed with {StatusCode}: {Message}",
                correlationId,
                statusCode,
                context.Exception.Message);
        }

        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = statusCode
        };
        context.ExceptionHandled = true;
    }

    private static ProblemDetails CreateProblemDetails(
        string title,
        string detail,
        int status,
        string correlationId,
        IDictionary<string, string[]>? errors = null)
    {
        var problemDetails = new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = status,
            Instance = correlationId
        };

        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        return problemDetails;
    }
}
```

---

## Estrategia de Cache

```csharp
// Infrastructure/Caching/CacheKeys.cs
public static class CacheKeys
{
    public static string AuctionState(Guid auctionId) => $"auction:state:{auctionId}";
    public static string AuctionDetails(Guid auctionId) => $"auction:details:{auctionId}";
    public static string UserProfile(string userId) => $"user:profile:{userId}";
    public static string ActiveAuctions(int page, int pageSize) => $"auctions:active:{page}:{pageSize}";
    public static string Idempotency(string key) => $"idempotency:{key}";
    public static string RateLimit(string key) => $"ratelimit:{key}";
}

public static class CacheDurations
{
    public static readonly TimeSpan AuctionState = TimeSpan.FromSeconds(5);      // Frequently changing
    public static readonly TimeSpan AuctionDetails = TimeSpan.FromMinutes(1);    // Semi-static
    public static readonly TimeSpan UserProfile = TimeSpan.FromMinutes(5);       // Rarely changes
    public static readonly TimeSpan ActiveAuctions = TimeSpan.FromSeconds(30);   // List changes often
    public static readonly TimeSpan Idempotency = TimeSpan.FromHours(24);        // Long retention
}
```

```csharp
// Infrastructure/Caching/CacheInvalidationService.cs
public interface ICacheInvalidationService
{
    Task InvalidateAuctionAsync(Guid auctionId);
    Task InvalidateUserAsync(string userId);
    Task InvalidateActiveAuctionsListAsync();
}

public class CacheInvalidationService : ICacheInvalidationService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CacheInvalidationService> _logger;

    public async Task InvalidateAuctionAsync(Guid auctionId)
    {
        var tasks = new[]
        {
            _cache.RemoveAsync(CacheKeys.AuctionState(auctionId)),
            _cache.RemoveAsync(CacheKeys.AuctionDetails(auctionId))
        };

        await Task.WhenAll(tasks);

        // Also invalidate list cache
        await InvalidateActiveAuctionsListAsync();

        _logger.LogDebug("Cache invalidated for auction {AuctionId}", auctionId);
    }

    public async Task InvalidateUserAsync(string userId)
    {
        await _cache.RemoveAsync(CacheKeys.UserProfile(userId));
        _logger.LogDebug("Cache invalidated for user {UserId}", userId);
    }

    public async Task InvalidateActiveAuctionsListAsync()
    {
        // Invalidate all pages of active auctions list using pattern
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());

        var keys = server.Keys(pattern: "auctions:active:*").ToArray();
        if (keys.Length > 0)
        {
            await db.KeyDeleteAsync(keys);
        }
    }
}
```

```csharp
// Usage in Query Handler
public class GetAuctionDetailsQueryHandler
    : IQueryHandler<GetAuctionDetailsQuery, AuctionDetailsDto>
{
    private readonly IDistributedCache _cache;
    private readonly IAuctionReadRepository _repository;

    public async Task<AuctionDetailsDto?> Handle(
        GetAuctionDetailsQuery query,
        CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.AuctionDetails(query.AuctionId);

        // Try cache first
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<AuctionDetailsDto>(cached);
        }

        // Fetch from database
        var auction = await _repository.GetDetailsAsync(query.AuctionId, cancellationToken);
        if (auction is null)
            return null;

        // Cache the result
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(auction),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDurations.AuctionDetails
            },
            cancellationToken);

        return auction;
    }
}
```

---

## Testes

### Teste de Concorrencia

```csharp
// tests/AuctionKings.IntegrationTests/ConcurrencyTests.cs
public class BidConcurrencyTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    [Fact]
    public async Task PlaceBid_ConcurrentBids_OnlyOneSucceeds_OthersGetConflict()
    {
        // Arrange
        var auctionId = await CreateTestAuctionAsync(startingPrice: 100m);
        var userIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();

        // Act - Submit 10 concurrent bids with same amount
        var tasks = userIds.Select(userId =>
            PlaceBidAsync(auctionId, userId, amount: 150m));

        var results = await Task.WhenAll(tasks);

        // Assert
        var successful = results.Count(r => r.IsSuccess);
        var conflicts = results.Count(r =>
            r.Error.Code == DomainErrors.Bid.ConcurrentModification.Code);

        // Exactly one should succeed
        Assert.Equal(1, successful);

        // Others should get concurrency error
        Assert.Equal(9, conflicts);

        // Final price should be 150
        var auction = await GetAuctionAsync(auctionId);
        Assert.Equal(150m, auction.CurrentPrice);
    }

    [Fact]
    public async Task PlaceBid_ConcurrentBids_DifferentAmounts_HighestWins()
    {
        // Arrange
        var auctionId = await CreateTestAuctionAsync(startingPrice: 100m);

        // Act - Submit concurrent bids with different amounts
        var tasks = new[]
        {
            PlaceBidAsync(auctionId, Guid.NewGuid(), amount: 150m),
            PlaceBidAsync(auctionId, Guid.NewGuid(), amount: 200m),
            PlaceBidAsync(auctionId, Guid.NewGuid(), amount: 175m)
        };

        var results = await Task.WhenAll(tasks);

        // Assert - All should eventually succeed (with retries)
        var auction = await GetAuctionAsync(auctionId);

        // Final price should be highest bid
        Assert.Equal(200m, auction.CurrentPrice);
    }
}
```

### Teste de Idempotencia

```csharp
// tests/AuctionKings.IntegrationTests/IdempotencyTests.cs
public class IdempotencyTests : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task PlaceBid_SameRequestId_ReturnsIdenticalResult()
    {
        // Arrange
        var auctionId = await CreateTestAuctionAsync(startingPrice: 100m);
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        // Act - Submit same bid twice
        var result1 = await PlaceBidAsync(auctionId, userId, 150m, requestId);
        var result2 = await PlaceBidAsync(auctionId, userId, 150m, requestId);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(result1.Value.BidId, result2.Value.BidId);

        // Verify only one bid was created
        var bids = await GetBidsForAuctionAsync(auctionId);
        Assert.Single(bids);
    }

    [Fact]
    public async Task PlaceBid_DifferentRequestIds_CreatesSeparateBids()
    {
        // Arrange
        var auctionId = await CreateTestAuctionAsync(startingPrice: 100m);
        var userId = Guid.NewGuid();

        // Act - Submit bids with different request IDs
        var result1 = await PlaceBidAsync(auctionId, userId, 150m, Guid.NewGuid());
        var result2 = await PlaceBidAsync(auctionId, userId, 200m, Guid.NewGuid());

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.NotEqual(result1.Value.BidId, result2.Value.BidId);

        // Verify two bids were created
        var bids = await GetBidsForAuctionAsync(auctionId);
        Assert.Equal(2, bids.Count);
    }
}
```

### Teste de Arquitetura

```csharp
// tests/AuctionKings.ArchitectureTests/DependencyTests.cs
public class ArchitectureDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Auction).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(PlaceBidCommand).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AuctionDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApplicationAssembly.GetName().Name)
            .GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypeNames?.FirstOrDefault());
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureAssembly.GetName().Name)
            .GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypeNames?.FirstOrDefault());
    }

    [Fact]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureAssembly.GetName().Name)
            .GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypeNames?.FirstOrDefault());
    }

    [Fact]
    public void CommandHandlers_ShouldHaveCorrectNaming()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .Should()
            .HaveNameEndingWith("CommandHandler")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
```

### Load Test (k6)

```javascript
// tests/AuctionKings.LoadTests/k6/bid-storm.js
import http from "k6/http";
import { check, sleep } from "k6";
import { randomIntBetween } from "https://jslib.k6.io/k6-utils/1.2.0/index.js";

export const options = {
  stages: [
    { duration: "1m", target: 50 }, // Ramp up
    { duration: "3m", target: 100 }, // Stay at 100 users
    { duration: "2m", target: 200 }, // Push to 200
    { duration: "3m", target: 200 }, // Stay at peak
    { duration: "1m", target: 0 }, // Ramp down
  ],
  thresholds: {
    http_req_duration: ["p(95)<500", "p(99)<1000"],
    http_req_failed: ["rate<0.01"],
  },
};

const BASE_URL = __ENV.BASE_URL || "http://localhost:5000";
const AUCTION_ID = __ENV.AUCTION_ID || "test-auction-id";

export default function () {
  const userId = `user-${__VU}-${__ITER}`;
  const requestId = `req-${Date.now()}-${randomIntBetween(1, 100000)}`;
  const bidAmount = randomIntBetween(100, 10000);

  const payload = JSON.stringify({
    userId: userId,
    amount: bidAmount,
    requestId: requestId,
  });

  const params = {
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${__ENV.AUTH_TOKEN}`,
    },
  };

  const response = http.post(
    `${BASE_URL}/api/v1/auctions/${AUCTION_ID}/bids`,
    payload,
    params,
  );

  check(response, {
    "status is 201 or 409": (r) => r.status === 201 || r.status === 409,
    "response time < 500ms": (r) => r.timings.duration < 500,
  });

  sleep(randomIntBetween(1, 3));
}
```

---

## CI/CD Pipeline

```yaml
# deploy/github-actions/ci.yml
name: CI Pipeline

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

env:
  DOTNET_VERSION: "10.0.x"
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_USER: test
          POSTGRES_PASSWORD: test
          POSTGRES_DB: auctionkings_test
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

      redis:
        image: redis:7
        ports:
          - 6379:6379
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Run Unit Tests
        run: |
          dotnet test tests/AuctionKings.UnitTests \
            --no-build \
            --configuration Release \
            --logger "trx;LogFileName=unit-tests.trx" \
            --collect:"XPlat Code Coverage"

      - name: Run Architecture Tests
        run: |
          dotnet test tests/AuctionKings.ArchitectureTests \
            --no-build \
            --configuration Release

      - name: Run Integration Tests
        env:
          ConnectionStrings__PostgreSQL: "Host=localhost;Database=auctionkings_test;Username=test;Password=test"
          ConnectionStrings__Redis: "localhost:6379"
        run: |
          dotnet test tests/AuctionKings.IntegrationTests \
            --no-build \
            --configuration Release \
            --logger "trx;LogFileName=integration-tests.trx"

      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: "**/TestResults/*.trx"

      - name: Upload Coverage
        uses: codecov/codecov-action@v4
        with:
          files: "**/coverage.cobertura.xml"
          fail_ci_if_error: true

  security-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Run Trivy vulnerability scanner
        uses: aquasecurity/trivy-action@master
        with:
          scan-type: "fs"
          scan-ref: "."
          severity: "CRITICAL,HIGH"

  build-image:
    needs: [build-and-test, security-scan]
    runs-on: ubuntu-latest
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'

    steps:
      - uses: actions/checkout@v4

      - name: Log in to Container Registry
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push API image
        uses: docker/build-push-action@v5
        with:
          context: .
          file: docker/Dockerfile.api
          push: true
          tags: |
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}-api:${{ github.sha }}
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}-api:latest

      - name: Build and push Worker image
        uses: docker/build-push-action@v5
        with:
          context: .
          file: docker/Dockerfile.worker
          push: true
          tags: |
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}-worker:${{ github.sha }}
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}-worker:latest
```

---

## Checklist de Implementacao

### Fase 1 - Fundacao

- [ ] Criar solucao e projetos .NET
- [ ] Configurar Directory.Build.props e Directory.Packages.props
- [ ] Implementar Domain Layer (Auction, Bid, Value Objects)
- [ ] Implementar Result pattern e Domain Errors
- [ ] Configurar PostgreSQL com EF Core
- [ ] Implementar Repositories basicos
- [ ] Criar migrations iniciais

### Fase 2 - Application Layer

- [ ] Configurar MediatR
- [ ] Implementar Commands e Handlers
- [ ] Implementar Queries e Handlers
- [ ] Adicionar FluentValidation
- [ ] Implementar Pipeline Behaviors (Logging, Validation)
- [ ] Configurar AutoMapper ou Mapster para DTOs

### Fase 3 - Concorrencia e Consistencia

- [ ] Implementar Outbox Pattern
- [ ] Criar OutboxProcessor BackgroundService
- [ ] Implementar Idempotency Behavior
- [ ] Configurar concorrencia otimista com xmin
- [ ] Testar cenarios de concorrencia

### Fase 4 - Event Sourcing

- [ ] Configurar DynamoDB
- [ ] Implementar EventStore
- [ ] Implementar Snapshots
- [ ] Criar Event projections
- [ ] Testar rebuild de agregados

### Fase 5 - Real-Time

- [ ] Configurar SignalR com Redis backplane
- [ ] Implementar AuctionHub
- [ ] Implementar state sync na reconexao
- [ ] Configurar graceful shutdown
- [ ] Testar broadcast sob carga

### Fase 6 - Seguranca

- [ ] Configurar AWS Cognito
- [ ] Implementar JWT authentication
- [ ] Configurar Authorization Policies
- [ ] Implementar Token Blacklist
- [ ] Configurar rate limiting distribuido

### Fase 7 - Observabilidade

- [ ] Configurar Prometheus metrics
- [ ] Implementar Health Checks
- [ ] Configurar structured logging
- [ ] Adicionar correlation IDs
- [ ] Criar dashboards Grafana

### Fase 8 - Testes

- [ ] Unit tests para Domain
- [ ] Unit tests para Application
- [ ] Integration tests com TestContainers
- [ ] Architecture tests
- [ ] Load tests com k6

### Fase 9 - Deploy

- [ ] Criar Dockerfiles
- [ ] Configurar Terraform
- [ ] Configurar CI/CD pipelines
- [ ] Documentar runbooks
- [ ] Realizar load testing em staging

---

## Conclusao

Esta arquitetura v2.0 corrige os problemas identificados na versao original:

| Problema Original              | Solucao Implementada              |
| ------------------------------ | --------------------------------- |
| Concorrencia em memoria        | Concorrencia a nivel de DB (xmin) |
| Dual-write sem transacao       | Outbox Pattern                    |
| Saga sem persistencia          | Saga State Table                  |
| Rate limit por instancia       | Redis distributed rate limiter    |
| Event sourcing sem snapshot    | Snapshots a cada 50 eventos       |
| SignalR sem sync de estado     | State sync na reconexao           |
| Token em query string          | Header/Cookie prioritarios        |
| Unique constraint problematico | Removido, usando idempotency key  |

O sistema esta preparado para:

- Escalar horizontalmente (stateless services)
- Manter consistencia sob alta concorrencia
- Recuperar de falhas (sagas, outbox, DLQ)
- Fornecer auditoria completa (event sourcing)
- Monitorar e alertar proativamente

---

**Autor**: Arquiteto de Software
**Data**: Janeiro 2026
**Versao**: 2.0
**Stack**: .NET 10 | ASP.NET Core | SignalR | Blazor | AWS | PostgreSQL | DynamoDB | Redis

```

```

```

```

anter consistencia sob alta concorrencia

- Recuperar de falhas (sagas, outbox, DLQ)
- Fornecer auditoria completa (event sourcing)
- Monitorar e alertar proativamente

---

**Autor**: Arquiteto de Software
**Data**: Janeiro 2026
**Versao**: 2.0
**Stack**: .NET 10 | ASP.NET Core | SignalR | Blazor | AWS | PostgreSQL | DynamoDB | Redis

```

```

```

```
