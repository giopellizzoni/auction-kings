using Microsoft.AspNetCore.SignalR.Client;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

namespace AuctionKings.LoadTests.Scenarios;

// Requires API running at http://localhost:5081 with DB and Redis.
// Run manually: dotnet test test/AuctionKings.LoadTests/
// NOT included in CI by default.
public sealed class SignalRLoadTest : LoadTestBase
{
    private const int ConcurrentConnections = 50;
    private const int DurationSeconds = 30;
    private const int WarmUpSeconds = 5;

    [Fact]
    public void SignalRLoadTest_ShouldHandleManyConnections()
    {
        Guid sharedAuctionId = Guid.Empty;

        var scenario = Scenario
            .Create(
                "signalr_connections",
                async _ =>
                {
                    var connection = new HubConnectionBuilder()
                        .WithUrl($"{BaseUrl}/hubs/auction")
                        .Build();

                    var stateReceived = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    );

                    connection.On<object>("AuctionState", _ => stateReceived.TrySetResult(true));

                    try
                    {
                        await connection.StartAsync();
                        await connection.InvokeAsync("JoinAuctionAsync", sharedAuctionId);

                        // Wait up to 2s for the server to push AuctionState
                        var winner = await Task.WhenAny(
                            stateReceived.Task,
                            Task.Delay(TimeSpan.FromSeconds(2))
                        );

                        return winner == stateReceived.Task
                            ? Response.Ok()
                            : Response.Fail(
                                statusCode: "timeout",
                                message: "AuctionState not received within 2s",
                                sizeBytes: 0
                            );
                    }
                    catch (Exception ex)
                    {
                        return Response.Fail(
                            statusCode: "error",
                            message: ex.Message,
                            sizeBytes: 0
                        );
                    }
                    finally
                    {
                        await connection.DisposeAsync();
                    }
                }
            )
            .WithInit(async _ =>
            {
                var initClient = CreateHttpClient();
                sharedAuctionId = await CreateAuctionAsync(initClient);
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(WarmUpSeconds))
            .WithLoadSimulations(
                Simulation.KeepConstant(
                    copies: ConcurrentConnections,
                    during: TimeSpan.FromSeconds(DurationSeconds)
                )
            );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("./reports")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Txt)
            .Run();

        var s = stats.ScenarioStats.First(x => x.ScenarioName == "signalr_connections");
        var errorPct = s.AllRequestCount > 0
            ? s.AllFailCount / (double)s.AllRequestCount * 100.0
            : 0.0;

        Assert.True(
            errorPct < 1.0,
            $"Error rate {errorPct:F2}% exceeded 1% threshold. "
                + $"Failed connections: {s.AllFailCount}/{s.AllRequestCount}"
        );
    }
}
