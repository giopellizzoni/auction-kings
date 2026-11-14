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

/// <summary>An <see cref="IValidateOptions{TOptions}"/> built from a list of checks, one message per failed check.</summary>
internal sealed class WorkerOptionsValidator<TOptions>(Func<TOptions, IEnumerable<string?>> check)
    : IValidateOptions<TOptions>
    where TOptions : class
{
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        var failures = check(options).OfType<string>().ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>Checks that return a failure message, or null when the setting is fine. Never echo the value.</summary>
internal static class Check
{
    public static string? Required(string section, string property, string? value) =>
        string.IsNullOrWhiteSpace(value) ? $"'{section}:{property}' is not configured." : null;

    public static string? Positive(string section, string property, int value) =>
        value > 0 ? null : $"'{section}:{property}' must be greater than zero.";
}
