using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AuctionKings.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations when the host starts. Opt-in through
/// <see cref="MigrateOnStartupKey"/>: on in <c>appsettings.Development.json</c> and in the staging ECS
/// task, off elsewhere, so a deployed environment only changes its schema when it is configured to.
/// </summary>
public static partial class DatabaseMigrator
{
    /// <summary>Configuration key that enables migration on startup.</summary>
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    /// <summary>
    /// Applies every pending migration. EF Core takes a database lock while migrating, so several
    /// replicas starting at once apply each migration only once.
    /// </summary>
    public static async Task MigrateDatabaseAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseMigrator));

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            LogUpToDate(logger);
            return;
        }

        var names = string.Join(", ", pending);
        LogApplying(logger, pending.Count, names);
        await context.Database.MigrateAsync(cancellationToken);
        LogApplied(logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is up to date; no migrations to apply.")]
    private static partial void LogUpToDate(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} pending migration(s): {Migrations}. Migrating (another replica starting at the same time may apply them first).")]
    private static partial void LogApplying(ILogger logger, int count, string migrations);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migration finished; the schema is up to date.")]
    private static partial void LogApplied(ILogger logger);
}
