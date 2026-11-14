using AuctionKings.BackgroundWorkers.Workers;
using AuctionKings.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuctionKings.BackgroundWorkers;

internal static class WorkerServiceCollectionExtensions
{
    /// <summary>
    /// Binds the worker options and validates them (plus the connection strings) when the host starts,
    /// so misconfiguration stops the process at launch instead of at the first poll.
    /// </summary>
    public static IServiceCollection AddWorkerOptions(this IServiceCollection services, IConfiguration configuration)
    {
        const string connections = WorkerConnectionStrings.SectionName;

        AddValidated<WorkerConnectionStrings>(
            services,
            configuration,
            connections,
            o =>
            [
                Check.Required(connections, nameof(o.DefaultConnection), o.DefaultConnection),
                Check.Required(connections, nameof(o.Redis), o.Redis),
            ]
        );

        AddValidated<OutboxOptions>(
            services,
            configuration,
            OutboxOptions.SectionName,
            o =>
            [
                Check.Positive(OutboxOptions.SectionName, nameof(o.PollingIntervalSeconds), o.PollingIntervalSeconds),
                Check.Positive(OutboxOptions.SectionName, nameof(o.BatchSize), o.BatchSize),
                Check.Positive(OutboxOptions.SectionName, nameof(o.MaxRetries), o.MaxRetries),
            ]
        );

        AddValidated<AuctionCloseWorkerOptions>(
            services,
            configuration,
            AuctionCloseWorkerOptions.SectionName,
            o =>
            [
                Check.Positive(AuctionCloseWorkerOptions.SectionName, nameof(o.PollingIntervalSeconds), o.PollingIntervalSeconds),
                Check.Positive(AuctionCloseWorkerOptions.SectionName, nameof(o.BatchSize), o.BatchSize),
            ]
        );

        AddValidated<AuctionActivationWorkerOptions>(
            services,
            configuration,
            AuctionActivationWorkerOptions.SectionName,
            o =>
            [
                Check.Positive(AuctionActivationWorkerOptions.SectionName, nameof(o.PollingIntervalSeconds), o.PollingIntervalSeconds),
                Check.Positive(AuctionActivationWorkerOptions.SectionName, nameof(o.BatchSize), o.BatchSize),
            ]
        );

        AddValidated<CleanupWorkerOptions>(
            services,
            configuration,
            CleanupWorkerOptions.SectionName,
            o =>
            [
                Check.Positive(CleanupWorkerOptions.SectionName, nameof(o.IntervalHours), o.IntervalHours),
                Check.Positive(CleanupWorkerOptions.SectionName, nameof(o.OutboxRetentionDays), o.OutboxRetentionDays),
            ]
        );

        return services;
    }

    private static void AddValidated<TOptions>(
        IServiceCollection services,
        IConfiguration configuration,
        string section,
        Func<TOptions, IEnumerable<string?>> check
    )
        where TOptions : class
    {
        services.AddSingleton<IValidateOptions<TOptions>>(new WorkerOptionsValidator<TOptions>(check));
        services.AddOptions<TOptions>().Bind(configuration.GetSection(section)).ValidateOnStart();
    }
}
