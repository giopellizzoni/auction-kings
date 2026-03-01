using AuctionKings.Domain.Aggregates;
using AuctionKings.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.IntegrationTests.Persistence;

[Collection("PostgreSql")]
public sealed class AuctionConfigurationTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public AuctionConfigurationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public void AuctionConfiguration_ShouldMapColumnsCorrectly()
    {
        using var context = _fixture.CreateDbContext();
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(Auction));

        Assert.NotNull(entityType);
        Assert.Equal("auctions", entityType.GetTableName());

        var properties = entityType
            .GetProperties()
            .ToDictionary(p => p.Name, p => p.GetColumnName());

        Assert.Equal("id", properties["Id"]);
        Assert.Equal("title", properties["Title"]);
        Assert.Equal("description", properties["Description"]);
        Assert.Equal("start_time", properties["StartTime"]);
        Assert.Equal("end_time", properties["EndTime"]);
        Assert.Equal("status", properties["Status"]);
        Assert.Equal("seller_id", properties["SellerId"]);
        Assert.Equal("winner_id", properties["WinnerId"]);
        Assert.Equal("version", properties["Version"]);

        // ComplexProperty columns for Money value objects
        var complexProperties = entityType.GetComplexProperties().ToList();

        var startingPriceComplex = complexProperties.FirstOrDefault(cp =>
            cp.Name == nameof(Auction.StartingPrice)
        );
        Assert.NotNull(startingPriceComplex);

        var startingPriceCols = startingPriceComplex.ComplexType
            .GetProperties()
            .ToDictionary(p => p.Name, p => p.GetColumnName());

        Assert.Equal("starting_price_amount", startingPriceCols["Amount"]);
        Assert.Equal("starting_price_currency", startingPriceCols["Currency"]);

        var currentPriceComplex = complexProperties.FirstOrDefault(cp =>
            cp.Name == nameof(Auction.CurrentPrice)
        );
        Assert.NotNull(currentPriceComplex);

        var currentPriceCols = currentPriceComplex.ComplexType
            .GetProperties()
            .ToDictionary(p => p.Name, p => p.GetColumnName());

        Assert.Equal("current_price_amount", currentPriceCols["Amount"]);
        Assert.Equal("current_price_currency", currentPriceCols["Currency"]);
    }
}
