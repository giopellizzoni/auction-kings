namespace AuctionKings.IntegrationTests.Infrastructure;

[CollectionDefinition("Redis")]
public sealed class RedisCollection : ICollectionFixture<RedisFixture>;
