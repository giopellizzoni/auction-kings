using AuctionKings.Application;
using Mapster;

namespace AuctionKings.UnitTests.Application.Queries;

[CollectionDefinition(nameof(MapsterCollection))]
public sealed class MapsterCollection : ICollectionFixture<MapsterFixture>
{
}

public sealed class MapsterFixture
{
    public MapsterFixture()
    {
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(typeof(DependencyInjection).Assembly);
        config.Compile();
    }
}
