# NovaTech .NET Style Guide

## C# Naming Conventions

### General Rules

```csharp
// PascalCase: classes, methods, properties, constants, enums
public class UserService { }
public void CalculateTotal() { }
public string FirstName { get; set; }
public const int MaxRetryAttempts = 3;
public enum OrderStatus { Pending, Active, Closed }

// camelCase: local variables, parameters, private fields (with underscore prefix)
private readonly ILogger _logger;
private int _retryCount;
var userName = "Alice";
void CreateUser(string firstName, string lastName) { }

// Interfaces: I prefix
public interface IUserRepository { }
public interface IAuctionService { }
```

### Naming Examples

```csharp
// GOOD: Clear, descriptive names
public class AuctionRepository : IAuctionRepository { }
public async Task<Auction> GetByIdAsync(Guid auctionId) { }
private readonly IDbContext _dbContext;

// BAD: Avoid these patterns
public class auction_repo { }       // snake_case
public async Task Get(Guid id) { }  // too generic
private IDbContext dbContext;        // missing underscore prefix
public string UN { get; set; }      // abbreviations
```

## Project & File Organization

### Clean Architecture Layer Structure

```text
src/
├── Domain/           # Entities, value objects, domain events
├── Application/      # CQRS commands, queries, handlers, interfaces
├── Infrastructure/   # EF Core, repositories, external services
└── API/              # Controllers, middleware, filters
```

### File Naming

- One class per file, file name matches class name
- Feature folders over type folders in Application layer
- Group related files: `CreateAuction/CreateAuctionCommand.cs`, `CreateAuction/CreateAuctionHandler.cs`

## Code Style Conventions

### Properties & Fields

```csharp
// Properties: auto-properties preferred
public string Title { get; private set; }
public decimal CurrentBid { get; private set; }

// Required init (C# 9+)
public required string Name { get; init; }

// Private fields: underscore prefix, readonly when possible
private readonly IAuctionRepository _repository;
private readonly ILogger<AuctionService> _logger;
```

### Methods & Functions

```csharp
// Async methods: always suffix with Async
public async Task<Result<Auction>> GetAuctionAsync(Guid id, CancellationToken cancellationToken)
{
    // ...
}

// Expression-bodied for simple members
public override string ToString() => $"{Title} ({Id})";

// Deconstruct parameters with records
public record CreateAuctionRequest(string Title, decimal StartingBid, DateTime EndsAt);

// Default parameters
public async Task<List<Auction>> GetActiveAuctionsAsync(int page = 1, int pageSize = 20) { }
```

### Modern C# Features

```csharp
// Nullable reference types — always enabled
string? optionalName = null;
public string Name { get; set; } = string.Empty;

// Pattern matching
var message = auction.Status switch
{
    AuctionStatus.Active => "Bidding is open",
    AuctionStatus.Closed => "Auction has ended",
    _ => "Unknown status"
};

// Null-coalescing and null-conditional
var city = user?.Address?.City ?? "Unknown";

// Records for value objects and DTOs
public record Money(decimal Amount, string Currency);

// Primary constructors (C# 12)
public class AuctionService(IAuctionRepository repository, ILogger<AuctionService> logger)
{
    // ...
}
```

## ASP.NET Core Conventions

### Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuctionsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<AuctionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAuctionDetailsQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
}
```

### Middleware & Filters

```csharp
// Use middleware for cross-cutting concerns
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            // handle...
        }
    }
}
```

## XML Documentation

### Public APIs

```csharp
/// <summary>
/// Places a bid on an active auction.
/// </summary>
/// <param name="command">The bid details including auction ID and amount.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>
/// A <see cref="Result{BidDto}"/> containing the placed bid on success,
/// or an error if the auction is closed or the bid is too low.
/// </returns>
/// <exception cref="NotFoundException">Thrown when the auction does not exist.</exception>
public async Task<Result<BidDto>> Handle(PlaceBidCommand command, CancellationToken cancellationToken)
{
    // ...
}
```

### Component Documentation

```csharp
/// <summary>
/// Repository for auction aggregate persistence.
/// </summary>
/// <remarks>
/// Implements optimistic concurrency via EF Core row version tokens.
/// Use <see cref="GetByIdAsync"/> for single item retrieval
/// and <see cref="GetActiveAuctionsAsync"/> for paginated listings.
/// </remarks>
public class AuctionRepository : IAuctionRepository { }
```

## Style Checklist

- [ ] PascalCase for public members, camelCase with `_` prefix for private fields
- [ ] Async methods suffixed with `Async` and accept `CancellationToken`
- [ ] Nullable reference types enabled and respected
- [ ] Records used for DTOs and value objects
- [ ] One class per file, file name matches class name
- [ ] XML `///` documentation on all public APIs
- [ ] Controllers thin — logic delegated to handlers/services
- [ ] `const` or `static readonly` for magic values
- [ ] No suppressed warnings — fix the root cause
