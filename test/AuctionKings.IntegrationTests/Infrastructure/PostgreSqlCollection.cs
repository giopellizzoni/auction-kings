namespace AuctionKings.IntegrationTests.Infrastructure;

[CollectionDefinition("PostgreSql")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>;
