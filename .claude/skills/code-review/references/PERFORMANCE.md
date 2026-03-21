# Performance Review Patterns

## C# .NET Performance

### Avoid Unnecessary Work

```csharp
// BAD: Recalculating on every call
public IEnumerable<Item> GetFilteredItems(IEnumerable<Item> items, string filter)
{
    return items.Where(item => item.Name.ToLower().Contains(filter.ToLower()));
}

// GOOD: Normalize once
public IEnumerable<Item> GetFilteredItems(IEnumerable<Item> items, string filter)
{
    var normalizedFilter = filter.ToLower();
    return items.Where(item => item.Name.ToLower().Contains(normalizedFilter));
}
```

### Use Appropriate Data Structures

```csharp
// BAD: List lookup O(n)
var items = new List<Item> { new(1, "A"), new(2, "B") };
var item = items.FirstOrDefault(i => i.Id == targetId);

// GOOD: Dictionary lookup O(1) for repeated access
var itemsDict = items.ToDictionary(i => i.Id);
var item = itemsDict.TryGetValue(targetId, out var result) ? result : null;
```

### Use `ValueTask` for Async Operations

```csharp
// BAD: Allocates Task even for synchronous paths
public Task<int> GetCountAsync()
{
    return Task.FromResult(42);
}

// GOOD: Avoids allocation for synchronous paths
public ValueTask<int> GetCountAsync()
{
    return new ValueTask<int>(42);
}
```

### Batch Database Operations

```csharp
// BAD: Multiple round-trips
foreach (var item in items)
{
    await _dbContext.Items.AddAsync(item);
    await _dbContext.SaveChangesAsync();
}

// GOOD: Single SaveChanges call
foreach (var item in items)
{
    await _dbContext.Items.AddAsync(item);
}
await _dbContext.SaveChangesAsync();
```

## Entity Framework Core Performance

### Use `AsNoTracking()` for Read-Only Queries

```csharp
// BAD: Tracking overhead for reads
var auctions = await _dbContext.Auctions.ToListAsync();

// GOOD: No tracking for read-only operations
var auctions = await _dbContext.Auctions.AsNoTracking().ToListAsync();
```

### Avoid N+1 Queries

```csharp
// BAD: Lazy loading causes N+1 queries
var auctions = await _dbContext.Auctions.ToListAsync();
foreach (var auction in auctions)
{
    var bids = auction.Bids; // Triggers query per auction
}

// GOOD: Eager loading with Include
var auctions = await _dbContext.Auctions
    .Include(a => a.Bids)
    .ToListAsync();
```

### Project Only Required Fields

```csharp
// BAD: Loading entire entities
var auctions = await _dbContext.Auctions.ToListAsync();

// GOOD: Project to DTO with only needed fields
var auctionDtos = await _dbContext.Auctions
    .Select(a => new AuctionDto
    {
        Id = a.Id,
        Title = a.Title
    })
    .ToListAsync();
```

## Caching Strategy

### Cache Frequently Accessed Data

```csharp
// BAD: Querying database repeatedly
public async Task<Auction> GetAuctionAsync(Guid id)
{
    return await _dbContext.Auctions.FindAsync(id);
}

// GOOD: Cache with Redis
public async Task<Auction> GetAuctionAsync(Guid id)
{
    var cacheKey = $"auction:{id}";
    var cached = await _cache.GetAsync(cacheKey);
    if (cached != null)
        return JsonSerializer.Deserialize<Auction>(cached);

    var auction = await _dbContext.Auctions.FindAsync(id);
    await _cache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(auction));
    return auction;
}
```

## Memory Management

### Dispose Resources Properly

```csharp
// BAD: Resource leak
var connection = new SqlConnection(connectionString);
connection.Open();
var cmd = new SqlCommand("SELECT...", connection);
var reader = cmd.ExecuteReader();

// GOOD: Using statement ensures disposal
using (var connection = new SqlConnection(connectionString))
{
    connection.Open();
    using (var cmd = new SqlCommand("SELECT...", connection))
    using (var reader = cmd.ExecuteReader())
    {
        // Process results
    }
}

// EVEN BETTER: Using declaration (C# 8+)
using var connection = new SqlConnection(connectionString);
connection.Open();
using var cmd = new SqlCommand("SELECT...", connection);
using var reader = cmd.ExecuteReader();
```

### Avoid Boxing Value Types

```csharp
// BAD: Boxing allocations
var list = new List<object> { 42, 3.14, true };

// GOOD: Use generic collections
var ints = new List<int> { 42 };
var decimals = new List<decimal> { 3.14m };
var bools = new List<bool> { true };
```

### Use `StringBuilder` for String Concatenation

```csharp
// BAD: Multiple allocations
var result = "";
foreach (var item in items)
{
    result += item.ToString();
}

// GOOD: Single allocation
var sb = new StringBuilder();
foreach (var item in items)
{
    sb.Append(item);
}
var result = sb.ToString();
```

## Concurrency & Async

### Use `ConfigureAwait(false)` in Libraries

```csharp
// BAD: Can cause deadlocks in UI contexts
var result = await _service.GetDataAsync();

// GOOD: Release context for library code
var result = await _service.GetDataAsync().ConfigureAwait(false);
```

### Avoid Blocking Calls

```csharp
// BAD: Blocks threads
var result = service.GetDataAsync().Result;
var result2 = service.GetDataAsync().GetAwaiter().GetResult();

// GOOD: Async all the way
var result = await service.GetDataAsync();
```

## Performance Checklist

- [ ] No unnecessary LINQ evaluations
- [ ] Database queries are optimized (AsNoTracking, Include, projections)
- [ ] No N+1 query patterns
- [ ] Frequently accessed data is cached
- [ ] Resources are disposed properly (using statements)
- [ ] Async operations used for I/O
- [ ] No blocking calls on async code
- [ ] Boxing avoided with generics
- [ ] StringBuilder used for string operations
- [ ] Connection pooling configured for databases
