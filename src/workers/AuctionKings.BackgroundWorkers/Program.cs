using AuctionKings.BackgroundWorkers;
using AuctionKings.Infrastructure;
using AuctionKings.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<OutboxOptions>(
    builder.Configuration.GetSection(OutboxOptions.SectionName)
);

builder.Services.AddHostedService<OutboxProcessor>();

var host = builder.Build();
await host.RunAsync();
