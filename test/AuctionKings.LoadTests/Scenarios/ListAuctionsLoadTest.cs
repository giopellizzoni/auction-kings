using NBomber.Contracts.Stats;
using NBomber.CSharp;

namespace AuctionKings.LoadTests.Scenarios;

// Requires API running at http://localhost:5081 with DB and Redis.
// Run manually: dotnet test test/AuctionKings.LoadTests/
// NOT included in CI by default.
public sealed class ListAuctionsLoadTest : LoadTestBase
{
    private const int InjectionRatePerSecond = 200;
    private const int DurationSeconds = 30;
    private const int WarmUpSeconds = 5;

    [Fact]
    public void ListAuctionsLoadTest_ShouldHandleHighThroughput()
    {
        var httpClient = CreateHttpClient();

        var scenario = Scenario
            .Create(
                "list_auctions",
                async _ =>
                {
                    var response = await httpClient.GetAsync(
                        "/api/v1/auctions?page=1&pageSize=20"
                    );

                    return response.IsSuccessStatusCode
                        ? Response.Ok()
                        : Response.Fail(
                            statusCode: ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture),
                            message: response.ReasonPhrase ?? string.Empty,
                            sizeBytes: 0
                        );
                }
            )
            .WithWarmUpDuration(TimeSpan.FromSeconds(WarmUpSeconds))
            .WithLoadSimulations(
                Simulation.Inject(
                    rate: InjectionRatePerSecond,
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromSeconds(DurationSeconds)
                )
            );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("./reports")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Txt)
            .Run();

        var s = stats.ScenarioStats.First(x => x.ScenarioName == "list_auctions");
        var errorPct = (s.Ok.Request.Count + s.Fail.Request.Count) > 0
            ? s.Fail.Request.Count / (double)(s.Ok.Request.Count + s.Fail.Request.Count) * 100.0
            : 0.0;

        Assert.True(
            errorPct < 1.0,
            $"Error rate {errorPct:F2}% exceeded 1% threshold. Failed: {s.Fail.Request.Count}/{(s.Ok.Request.Count + s.Fail.Request.Count)}"
        );

        Assert.True(
            s.Ok.Latency.Percent99 < 500.0,
            $"P99 latency {s.Ok.Latency.Percent99:F1}ms exceeded 500ms threshold"
        );
    }
}
