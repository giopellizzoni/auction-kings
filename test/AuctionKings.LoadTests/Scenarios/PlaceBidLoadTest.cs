using System.Net.Http.Json;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

namespace AuctionKings.LoadTests.Scenarios;

// Requires API running at http://localhost:5081 with DB and Redis.
// Run manually: dotnet test test/AuctionKings.LoadTests/
// NOT included in CI by default (CI only runs UnitTests, ArchitectureTests, IntegrationTests).
public sealed class PlaceBidLoadTest : LoadTestBase
{
    private const int AuctionCount = 10;
    private const int ConcurrentUsers = 100;
    private const int DurationSeconds = 30;
    private const int WarmUpSeconds = 5;

    [Fact]
    public void PlaceBidLoadTest_ShouldHandleConcurrentBids()
    {
        var httpClient = CreateHttpClient();
        var auctionIds = new Guid[AuctionCount];
        var bidAmountBase = 0;

        var scenario = Scenario
            .Create(
                "place_bid",
                async _ =>
                {
                    var auctionId = auctionIds[Random.Shared.Next(AuctionCount)];
                    // Monotonically increasing amount reduces "bid too low" failures
                    var amount = 10.00m + (Interlocked.Increment(ref bidAmountBase) * 0.01m);

                    var request = new
                    {
                        BidderId = Guid.NewGuid(),
                        Amount = amount,
                        Currency = "USD",
                    };

                    using var message = new HttpRequestMessage(
                        HttpMethod.Post,
                        $"/api/v1/auctions/{auctionId}/bids"
                    );
                    message.Content = JsonContent.Create(request);
                    message.Headers.Add("X-Request-Id", Guid.NewGuid().ToString());

                    var response = await httpClient.SendAsync(message);

                    return response.IsSuccessStatusCode
                        ? Response.Ok()
                        : Response.Fail(
                            statusCode: ((int)response.StatusCode).ToString(
                                System.Globalization.CultureInfo.InvariantCulture
                            ),
                            message: await response.Content.ReadAsStringAsync(),
                            sizeBytes: 0
                        );
                }
            )
            .WithInit(async _ =>
            {
                var initClient = CreateHttpClient();
                for (var i = 0; i < AuctionCount; i++)
                {
                    auctionIds[i] = await CreateAuctionAsync(initClient);
                }
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(WarmUpSeconds))
            .WithLoadSimulations(
                Simulation.KeepConstant(
                    copies: ConcurrentUsers,
                    during: TimeSpan.FromSeconds(DurationSeconds)
                )
            );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("./reports")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Txt)
            .Run();

        var s = stats.ScenarioStats.First(x => x.ScenarioName == "place_bid");
        var errorPct =
            (s.Ok.Request.Count + s.Fail.Request.Count) > 0 ? s.Fail.Request.Count / (double)(s.Ok.Request.Count + s.Fail.Request.Count) * 100.0 : 0.0;

        // Concurrent bids on shared auctions produce some concurrency conflicts (expected).
        // Assert on latency: even under contention the API must stay responsive.
        Assert.True(
            s.Ok.Latency.Percent99 < 500.0,
            $"P99 latency {s.Ok.Latency.Percent99:F1}ms exceeded 500ms threshold. "
                + $"Total: {(s.Ok.Request.Count + s.Fail.Request.Count)}, OK: {s.Ok.Request.Count}, Fail: {s.Fail.Request.Count} ({errorPct:F2}%)"
        );
    }
}
