using System.Net.Http.Json;

namespace AuctionKings.LoadTests;

public abstract class LoadTestBase
{
    protected const string BaseUrl = "http://localhost:5081";

    protected static HttpClient CreateHttpClient() =>
        new() { BaseAddress = new Uri(BaseUrl) };

    protected static async Task<Guid> CreateAuctionAsync(HttpClient client)
    {
        var request = new
        {
            Title = $"Load Test Auction {Guid.NewGuid():N}",
            Description = "Auction created during load testing",
            StartingPrice = 10.00m,
            Currency = "USD",
            DurationInHours = 2.0,
            SellerId = Guid.NewGuid(),
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auctions");
        message.Content = JsonContent.Create(request);
        message.Headers.Add("X-Request-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }
}
