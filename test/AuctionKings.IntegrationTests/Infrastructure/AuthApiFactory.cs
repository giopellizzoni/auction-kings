using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuctionKings.API.Auth;
using AuctionKings.API.Controllers;
using AuctionKings.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AuctionKings.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API (Program.cs, real authentication/authorization pipeline, real hubs) against
/// throwaway Postgres and Redis containers, so authorization tests exercise exactly what ships.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<AuctionsController>, IAsyncLifetime
{
    /// <summary>Test-only signing secret. Never used outside this test host.</summary>
    internal const string TestJwtSecret = "integration-tests-only-signing-key-0123456789-not-a-real-secret";

    internal const string TestIssuer = "auction-kings-tests";
    internal const string TestAudience = "auction-kings-tests";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("auction_kings_auth_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    /// <summary>Connection string of the Redis container the host under test uses.</summary>
    internal string RedisConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var context = new AuctionDbContext(options);
        await context.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development: also maps the OpenAPI/Scalar endpoints, so the endpoint audit sees them.
        builder.UseEnvironment("Development");

        // UseSetting feeds the host configuration, which Program.cs reads eagerly while building.
        builder.UseSetting("Jwt:Secret", TestJwtSecret);
        builder.UseSetting("Jwt:Issuer", TestIssuer);
        builder.UseSetting("Jwt:Audience", TestAudience);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("Features:EnableDevTokenEndpoint", "false");
    }

    /// <summary>A token signed with the secret the host validates against.</summary>
    public string CreateToken(Guid userId, string role) =>
        Services.GetRequiredService<JwtTokenGenerator>().Generate(userId, role);

    /// <summary>A token with correct claims, issuer and audience but signed with a different key.</summary>
    public static string CreateTokenWithWrongKey(Guid userId, string role)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("a-completely-different-key-0123456789-abcdef")
        );
        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

[CollectionDefinition("AuthApi")]
public sealed class AuthApiTestFixtures : ICollectionFixture<AuthApiFactory>;
