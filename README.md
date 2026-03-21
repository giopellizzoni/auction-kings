# AuctionKings

A modern, real-time auction platform built with ASP.NET Core, featuring WebSocket-based bidding, Domain-Driven Design architecture, and comprehensive testing.

## Tech Stack

- **Framework**: ASP.NET Core 10.0
- **Language**: C# with nullable reference types
- **Architecture**: Clean Architecture with Domain-Driven Design (DDD)
- **Real-time Communication**: SignalR (WebSockets)
- **Database**: PostgreSQL 16
- **Cache**: Redis 7
- **Testing**: xUnit with Coverlet
- **Containerization**: Docker & Docker Compose

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## Project Structure

```
auction-kings/
├── src/
│   ├── services/
│   │   ├── AuctionKings.API/              # REST API & SignalR Hubs
│   │   ├── AuctionKings.Application/      # Business Logic Layer
│   │   ├── AuctionKings.Domain/           # Domain Models & Entities
│   │   └── AuctionKings.Infrastructure/   # Data Access & External Services
│   ├── common/
│   │   └── AuctionKings.Shared/           # Shared Utilities
│   └── workers/
│       └── AuctionKings.BackgroundWorkers/ # Background Jobs
├── test/
│   ├── AuctionKings.UnitTests/
│   ├── AuctionKings.IntegrationTests/
│   ├── AuctionKings.ArchitectureTests/
│   └── AuctionKings.LoadTests/
└── docker/                                 # Docker configurations
```

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd auction-kings
```

### 2. Setup Database Credentials

The project uses Docker secrets for secure password management. Password files already exist in `docker/secrets/`:
- `postgres_password.txt` - PostgreSQL password
- `redis_password.txt` - Redis password

If you need to change them, edit these files directly.

### 3. Start Infrastructure Services

Start PostgreSQL and Redis using Docker Compose:

```bash
cd docker
docker-compose -f docker-compose.infra.yml up -d
```

Verify services are running:

```bash
docker ps
```

You should see:
- `infra-postgres` (PostgreSQL 16) on port 5432
- `infra-redis` (Redis 7) on port 6379

### 4. Restore Dependencies

From the project root:

```bash
dotnet restore
```

### 5. Build the Solution

```bash
dotnet build
```

### 6. Run the API

```bash
dotnet run --project src/services/AuctionKings.API/AuctionKings.API.csproj
```

The API will be available at:
- HTTP: `http://localhost:5081`
- HTTPS: `https://localhost:7145`
- Swagger UI: `http://localhost:5081/swagger` (Development environment)

### 7. Run Background Workers (Optional)

In a separate terminal:

```bash
dotnet run --project src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj
```

## Running Tests

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Projects

```bash
# Unit Tests
dotnet test test/AuctionKings.UnitTests/

# Integration Tests
dotnet test test/AuctionKings.IntegrationTests/

# Architecture Tests
dotnet test test/AuctionKings.ArchitectureTests/

# Load Tests
dotnet test test/AuctionKings.LoadTests/
```

### Run Tests with Coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## API Endpoints

The API includes the following controllers:

- **Auctions** (`/api/auctions`) - Manage auction listings
- **Bids** (`/api/bids`) - Place and manage bids
- **Users** (`/api/users`) - User management

### SignalR Hubs

Real-time features are available through SignalR hubs:

- **AuctionHub** - Real-time auction updates
- **NotificationHub** - Real-time notifications

## Configuration

### Database Connection

Database configuration is managed through:
- User: `dev`
- Database: `AuctionKingsDB`
- Host: `localhost`
- Port: `5432`
- Password: See `docker/secrets/postgres_password.txt`

### Redis Connection

Redis configuration:
- Host: `localhost`
- Port: `6379`
- Password: See `docker/secrets/redis_password.txt`

### Environment Variables

Key environment variables are in `docker/.env`:
- `POSTGRES_USER`, `POSTGRES_DB`, `POSTGRES_HOST_PORT`
- `REDIS_HOST_PORT`
- Password file paths for secrets

## Development

### Architecture Patterns

The project follows Clean Architecture principles with:
- **Entity & AggregateRoot** base classes for DDD
- **Repository Pattern** for data access
- **CQRS** considerations (separate read/write models)
- **SignalR Hubs** for real-time communication

### API Features

- **Global Exception Handling** - Centralized error handling
- **Rate Limiting** - Request throttling
- **Idempotency** - Duplicate request protection
- **OpenAPI/Swagger** - Interactive API documentation

## Stopping Services

### Stop Infrastructure

```bash
cd docker
docker-compose -f docker-compose.infra.yml down
```

### Stop with Volume Cleanup

```bash
cd docker
docker-compose -f docker-compose.infra.yml down -v
```

## Troubleshooting

### Port Already in Use

If ports 5432, 6379, 5081, or 7145 are already in use, stop the conflicting services or modify the ports in the configuration files.

### Database Connection Issues

Ensure PostgreSQL is healthy:

```bash
docker logs infra-postgres
```

### Redis Connection Issues

Check Redis status:

```bash
docker logs infra-redis
```

## Task Progress

| Task | Description | Status | Notes |
|------|-------------|--------|-------|
| [Task 01](tasks/Task01.md) | Base Classes: Entity, AggregateRoot, Result, Error | ✅ Completed | |
| [Task 02](tasks/Task02.md) | Domain Model: Auction aggregate, Bid, Value Objects, Events | ✅ Completed | |
| [Task 03](tasks/Task03.md) | Application Layer: MediatR, CQRS pipeline behaviors, interfaces | ✅ Completed | |
| [Task 04](tasks/Task04.md) | Commands: CreateAuction, PlaceBid, CloseAuction | ✅ Completed | |
| [Task 05](tasks/Task05.md) | Queries: GetAuctionDetails, GetActiveAuctions, GetAuctionBids | ✅ Completed | |
| [Task 06](tasks/Task06.md) | Infrastructure: EF Core, DbContext, Repositories, UnitOfWork | ✅ Completed | |
| [Task 07](tasks/Task07.md) | Outbox Processor and Idempotency | ✅ Completed | |
| [Task 08](tasks/Task08.md) | Redis Cache Service and Rate Limiter | ✅ Completed | |
| [Task 09](tasks/Task09.md) | SignalR Hubs: AuctionHub, NotificationHub | ⏳ Not Started | 🔴 [#43](https://github.com/PellizzoniCode/auction-kings/issues/43) [#44](https://github.com/PellizzoniCode/auction-kings/issues/44) [#45](https://github.com/PellizzoniCode/auction-kings/issues/45) [#46](https://github.com/PellizzoniCode/auction-kings/issues/46) [#47](https://github.com/PellizzoniCode/auction-kings/issues/47) |
| [Task 10](tasks/Task10.md) | API Controllers: Auctions, Bids, Users | ⏳ Not Started | 🔴 [#19](https://github.com/PellizzoniCode/auction-kings/issues/19) [#20](https://github.com/PellizzoniCode/auction-kings/issues/20) [#21](https://github.com/PellizzoniCode/auction-kings/issues/21) |
| [Task 11](tasks/Task11.md) | Middleware: Global Exception Filter, Correlation ID | ⏳ Not Started | 🔴 [#33](https://github.com/PellizzoniCode/auction-kings/issues/33) [#55](https://github.com/PellizzoniCode/auction-kings/issues/55) |
| [Task 12](tasks/Task12.md) | Health Checks: DB, Redis, OutboxProcessor | 🔄 In Progress | ✅ [#8](https://github.com/PellizzoniCode/auction-kings/issues/8) · 🔴 [#56](https://github.com/PellizzoniCode/auction-kings/issues/56) |
| [Task 13](tasks/Task13.md) | Background Workers: AuctionCloseWorker, CleanupWorker | 🔄 In Progress | ✅ [#37](https://github.com/PellizzoniCode/auction-kings/issues/37) |
| [Task 14](tasks/Task14.md) | Unit Tests: Domain, Application, Behaviors, Shared | ✅ Completed | |
| [Task 15](tasks/Task15.md) | Integration Tests with Testcontainers | 🔄 In Progress | ✅ [#60](https://github.com/PellizzoniCode/auction-kings/issues/60) · 🔴 [#39](https://github.com/PellizzoniCode/auction-kings/issues/39) [#40](https://github.com/PellizzoniCode/auction-kings/issues/40) |
| [Task 16](tasks/Task16.md) | Architecture Tests with NetArchTest | ⏳ Not Started | 🔴 [#61](https://github.com/PellizzoniCode/auction-kings/issues/61) |
| [Task 17](tasks/Task17.md) | CI Pipeline: GitHub Actions | 🔄 In Progress | 🔴 [#66](https://github.com/PellizzoniCode/auction-kings/issues/66) |
| [Task 18](tasks/Task18.md) | Load Tests with NBomber | ⏳ Not Started | 🔴 [#62](https://github.com/PellizzoniCode/auction-kings/issues/62) [#63](https://github.com/PellizzoniCode/auction-kings/issues/63) [#64](https://github.com/PellizzoniCode/auction-kings/issues/64) |

**Legend:** ✅ Completed · 🔄 In Progress · ⏳ Not Started

## Contributing

1. Create a feature branch from `develop`
2. Make your changes
3. Run tests: `dotnet test`
4. Submit a pull request

## License

[Add your license here]
