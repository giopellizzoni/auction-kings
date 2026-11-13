using AuctionKings.Infrastructure.Outbox;
using AuctionKings.BackgroundWorkers.Workers;
using Microsoft.Extensions.Options;

namespace AuctionKings.BackgroundWorkers;

/// <summary>
/// The connection strings the worker cannot run without. <c>AddInfrastructure</c> already throws when
/// a key is absent; this catches a key that is present but blank (e.g. <c>ConnectionStrings__Redis=</c>),
/// which would otherwise only fail on the first database or Redis call.
/// </summary>
internal sealed class WorkerConnectionStrings
{
    public const string SectionName = "ConnectionStrings";

    public string? DefaultConnection { get; init; }
    public string? Redis { get; init; }
}

internal sealed class WorkerConnectionStringsValidator : IValidateOptions<WorkerConnectionStrings>
{
    public ValidateOptionsResult Validate(string? name, WorkerConnectionStrings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DefaultConnection))
        {
            failures.Add($"'{WorkerConnectionStrings.SectionName}:DefaultConnection' is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Redis))
        {
            failures.Add($"'{WorkerConnectionStrings.SectionName}:Redis' is not configured.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>Polling intervals, batch sizes and retention must be positive, or a worker spins or never runs.</summary>
internal sealed class WorkerOptionsValidator
    : IValidateOptions<OutboxOptions>,
        IValidateOptions<AuctionCloseWorkerOptions>,
        IValidateOptions<AuctionActivationWorkerOptions>,
        IValidateOptions<CleanupWorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxOptions options) =>
        Result(
            Positive(OutboxOptions.SectionName, nameof(options.PollingIntervalSeconds), options.PollingIntervalSeconds),
            Positive(OutboxOptions.SectionName, nameof(options.BatchSize), options.BatchSize),
            Positive(OutboxOptions.SectionName, nameof(options.MaxRetries), options.MaxRetries)
        );

    public ValidateOptionsResult Validate(string? name, AuctionCloseWorkerOptions options) =>
        Result(
            Positive(AuctionCloseWorkerOptions.SectionName, nameof(options.PollingIntervalSeconds), options.PollingIntervalSeconds),
            Positive(AuctionCloseWorkerOptions.SectionName, nameof(options.BatchSize), options.BatchSize)
        );

    public ValidateOptionsResult Validate(string? name, AuctionActivationWorkerOptions options) =>
        Result(
            Positive(AuctionActivationWorkerOptions.SectionName, nameof(options.PollingIntervalSeconds), options.PollingIntervalSeconds),
            Positive(AuctionActivationWorkerOptions.SectionName, nameof(options.BatchSize), options.BatchSize)
        );

    public ValidateOptionsResult Validate(string? name, CleanupWorkerOptions options) =>
        Result(
            Positive(CleanupWorkerOptions.SectionName, nameof(options.IntervalHours), options.IntervalHours),
            Positive(CleanupWorkerOptions.SectionName, nameof(options.OutboxRetentionDays), options.OutboxRetentionDays)
        );

    private static string? Positive(string section, string property, int value) =>
        value > 0 ? null : $"'{section}:{property}' must be greater than zero.";

    private static ValidateOptionsResult Result(params string?[] failures)
    {
        var errors = failures.OfType<string>().ToList();
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
