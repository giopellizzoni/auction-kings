using AuctionKings.Application;
using Mapster;

namespace AuctionKings.UnitTests.Application.Queries;

[CollectionDefinition(nameof(MapsterTestFixtures))]
public sealed class MapsterTestFixtures : ICollectionFixture<MapsterFixture> { }

public sealed class MapsterFixture
{
    public MapsterFixture()
    {
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(typeof(DependencyInjection).Assembly);
        config.Compile();
    }
}
