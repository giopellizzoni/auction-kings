using AuctionKings.BackgroundWorkers;
using AuctionKings.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuctionKings.UnitTests.Infrastructure.Workers;

public sealed class WorkerStartupValidationTests
{
    // Fake values: the point is to prove they are never echoed back in an error message.
    private const string SecretPassword = "s3cr3t-pa55word";

    private static Dictionary<string, string?> ValidSettings() =>
        new()
        {
            ["ConnectionStrings:DefaultConnection"] = $"Host=localhost;Database=db;Username=dev;Password={SecretPassword}",
            ["ConnectionStrings:Redis"] = "localhost:6379",
            ["Outbox:PollingIntervalSeconds"] = "5",
            ["Outbox:BatchSize"] = "20",
            ["AuctionCloseWorker:PollingIntervalSeconds"] = "10",
            ["AuctionActivationWorker:BatchSize"] = "50",
            ["CleanupWorker:IntervalHours"] = "1",
        };

    private static IConfiguration Build(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static void RunStartupValidation(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddWorkerOptions(configuration);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void ValidConfiguration_PassesStartupValidation()
    {
        RunStartupValidation(Build(ValidSettings()));
    }

    [Fact]
    public void EmptyConfiguration_PassesForOptionsThatHaveDefaults_ButFailsForConnectionStrings()
    {
        var ex = Assert.Throws<OptionsValidationException>(() => RunStartupValidation(Build([])));

        Assert.Equal(
            [
                "'ConnectionStrings:DefaultConnection' is not configured.",
                "'ConnectionStrings:Redis' is not configured.",
            ],
            ex.Failures.Order());
    }

    [Theory]
    [InlineData("ConnectionStrings:DefaultConnection")]
    [InlineData("ConnectionStrings:Redis")]
    public void MissingOrBlankConnectionString_NamesTheKey(string key)
    {
        foreach (var value in new string?[] { null, "", "   " })
        {
            var settings = ValidSettings();
            if (value is null)
            {
                settings.Remove(key);
            }
            else
            {
                settings[key] = value;
            }

            var ex = Assert.Throws<OptionsValidationException>(() => RunStartupValidation(Build(settings)));

            Assert.Contains($"'{key}'", ex.Message);
        }
    }

    [Theory]
    [InlineData("Outbox:PollingIntervalSeconds", "0")]
    [InlineData("Outbox:BatchSize", "-1")]
    [InlineData("Outbox:MaxRetries", "0")]
    [InlineData("AuctionCloseWorker:PollingIntervalSeconds", "0")]
    [InlineData("AuctionCloseWorker:BatchSize", "0")]
    [InlineData("AuctionActivationWorker:PollingIntervalSeconds", "0")]
    [InlineData("AuctionActivationWorker:BatchSize", "-5")]
    [InlineData("CleanupWorker:IntervalHours", "0")]
    [InlineData("CleanupWorker:OutboxRetentionDays", "0")]
    public void NonPositiveWorkerSetting_FailsAndNamesTheKey(string key, string value)
    {
        var settings = ValidSettings();
        settings[key] = value;

        var ex = Assert.Throws<OptionsValidationException>(() => RunStartupValidation(Build(settings)));

        Assert.Contains($"'{key}' must be greater than zero", ex.Message);
    }

    [Fact]
    public void FailureMessages_NeverContainConnectionStringValues()
    {
        var settings = ValidSettings();
        settings["ConnectionStrings:Redis"] = "";
        settings["Outbox:BatchSize"] = "0";

        // Two options fail at once, so the host reports an AggregateException with one entry per option.
        var ex = Assert.Throws<AggregateException>(() => RunStartupValidation(Build(settings)));

        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.All(ex.InnerExceptions, inner => Assert.DoesNotContain(SecretPassword, inner.Message));
    }

    [Theory]
    [InlineData("ConnectionStrings:DefaultConnection")]
    [InlineData("ConnectionStrings:Redis")]
    public void AddInfrastructure_StillFailsOnAbsentConnectionString(string key)
    {
        // Absent keys are rejected by AddInfrastructure itself; the worker validator only adds the blank case.
        var settings = ValidSettings();
        settings.Remove(key);

        var ex = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddInfrastructure(Build(settings)));

        Assert.Contains(key.Split(':')[1], ex.Message);
    }
}
