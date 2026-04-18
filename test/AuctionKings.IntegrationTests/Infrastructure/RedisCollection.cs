namespace AuctionKings.IntegrationTests.Infrastructure;

[CollectionDefinition("Redis")]
public sealed class RedisTestFixtures : ICollectionFixture<RedisFixture>;
