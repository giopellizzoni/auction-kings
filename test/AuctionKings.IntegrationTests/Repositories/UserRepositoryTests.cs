using AuctionKings.Application.Exceptions;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Infrastructure.Persistence;
using AuctionKings.Infrastructure.Persistence.Configurations;
using AuctionKings.Infrastructure.Repositories;
using AuctionKings.IntegrationTests.Infrastructure;

namespace AuctionKings.IntegrationTests.Repositories;

[Collection("PostgreSql")]
public sealed class UserRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;

    public UserRepositoryTests(PostgreSqlFixture fixture) => _fixture = fixture;

    private static User NewUser(string email) =>
        User.Register(email, "Test User", "pbkdf2-sha512.1.AAAA.BBBB", DateTime.UtcNow).Value;

    [Fact]
    public async Task UserRepository_FindsUsersByEmail_IgnoringCaseAndSurroundingSpaces()
    {
        var email = $"find-{Guid.NewGuid():N}@example.com";
        await using (var context = _fixture.CreateDbContext())
        {
            await new UserRepository(context).AddAsync(NewUser(email));
            await new UnitOfWork(context).CommitAsync();
        }

        await using var readContext = _fixture.CreateDbContext();
        var repository = new UserRepository(readContext);

        var found = await repository.GetByEmailAsync($" {email.ToUpperInvariant()} ");

        Assert.NotNull(found);
        Assert.Equal(email, found.Email);
        Assert.True(await repository.EmailExistsAsync(email.ToUpperInvariant()));
        Assert.Null(await repository.GetByEmailAsync($"missing-{Guid.NewGuid():N}@example.com"));
    }

    [Fact]
    public async Task UnitOfWork_Commit_TranslatesADuplicateEmailIntoUniqueConstraintException()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await using (var first = _fixture.CreateDbContext())
        {
            await new UserRepository(first).AddAsync(NewUser(email));
            await new UnitOfWork(first).CommitAsync();
        }

        // A second request that passed its EmailExistsAsync check before the first one committed.
        await using var second = _fixture.CreateDbContext();
        await new UserRepository(second).AddAsync(NewUser(email));

        var ex = await Assert.ThrowsAsync<UniqueConstraintException>(() => new UnitOfWork(second).CommitAsync());
        Assert.Equal(UserConfiguration.EmailIndexName, ex.ConstraintName);
    }
}
