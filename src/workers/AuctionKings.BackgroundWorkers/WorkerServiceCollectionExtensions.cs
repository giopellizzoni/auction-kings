using AuctionKings.BackgroundWorkers.Workers;
using AuctionKings.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<WorkerConnectionStrings>, WorkerConnectionStringsValidator>());
        services.AddSingleton<WorkerOptionsValidator>();
        services.AddSingleton<IValidateOptions<OutboxOptions>>(sp => sp.GetRequiredService<WorkerOptionsValidator>());
        services.AddSingleton<IValidateOptions<AuctionCloseWorkerOptions>>(sp => sp.GetRequiredService<WorkerOptionsValidator>());
        services.AddSingleton<IValidateOptions<AuctionActivationWorkerOptions>>(sp => sp.GetRequiredService<WorkerOptionsValidator>());
        services.AddSingleton<IValidateOptions<CleanupWorkerOptions>>(sp => sp.GetRequiredService<WorkerOptionsValidator>());

        services.AddOptions<WorkerConnectionStrings>().Bind(configuration.GetSection(WorkerConnectionStrings.SectionName)).ValidateOnStart();
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.SectionName)).ValidateOnStart();
        services.AddOptions<AuctionCloseWorkerOptions>().Bind(configuration.GetSection(AuctionCloseWorkerOptions.SectionName)).ValidateOnStart();
        services.AddOptions<AuctionActivationWorkerOptions>().Bind(configuration.GetSection(AuctionActivationWorkerOptions.SectionName)).ValidateOnStart();
        services.AddOptions<CleanupWorkerOptions>().Bind(configuration.GetSection(CleanupWorkerOptions.SectionName)).ValidateOnStart();

        return services;
    }
}
