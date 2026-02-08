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

## Contributing

1. Create a feature branch from `develop`
2. Make your changes
3. Run tests: `dotnet test`
4. Submit a pull request

## License

[Add your license here]
