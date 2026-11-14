# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Copy project files for restore layer caching
COPY AuctionKings.slnx .
# .editorconfig carries the analyzer severities (e.g. CS8618 = none) that TreatWarningsAsErrors depends on;
# without it the image build fails on warnings the editorconfig silences.
COPY Directory.Build.props .editorconfig .
COPY Directory.Packages.props .
COPY src/common/AuctionKings.Shared/AuctionKings.Shared.csproj src/common/AuctionKings.Shared/
COPY src/services/AuctionKings.Domain/AuctionKings.Domain.csproj src/services/AuctionKings.Domain/
COPY src/services/AuctionKings.Application/AuctionKings.Application.csproj src/services/AuctionKings.Application/
COPY src/services/AuctionKings.Infrastructure/AuctionKings.Infrastructure.csproj src/services/AuctionKings.Infrastructure/
COPY src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj src/workers/AuctionKings.BackgroundWorkers/

RUN dotnet restore src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj \
    -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd --system appgroup && useradd --system --gid appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AuctionKings.BackgroundWorkers.dll"]
