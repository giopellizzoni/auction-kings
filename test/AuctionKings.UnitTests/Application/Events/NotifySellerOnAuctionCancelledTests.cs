using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Events;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Events;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AuctionKings.UnitTests.Application.Events;

public sealed class NotifySellerOnAuctionCancelledTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SellerId = Guid.NewGuid();

    private readonly IAuctionRepository _auctionRepository = Substitute.For<IAuctionRepository>();
    private readonly IRealtimeNotificationService _notificationService = Substitute.For<IRealtimeNotificationService>();
    private readonly NotifySellerOnAuctionCancelled _handler;

    public NotifySellerOnAuctionCancelledTests()
    {
        _handler = new NotifySellerOnAuctionCancelled(
            _auctionRepository,
            _notificationService,
            NullLogger<NotifySellerOnAuctionCancelled>.Instance);
    }

    private static Auction CreateAuction() =>
        Auction.Create("Vase", "Description", new Money(100m, "USD"), Now, AuctionDuration.FromDays(1), SellerId).Value;

    private static DomainEventNotification<AuctionCancelledEvent> Notification(Guid auctionId) =>
        new(new AuctionCancelledEvent(auctionId, Now));

    [Fact]
    public async Task Handle_ExistingAuction_ShouldNotifySellerExactlyOnce()
    {
        var auction = CreateAuction();
        _auctionRepository.GetByIdAsync(auction.Id, Arg.Any<CancellationToken>()).Returns(auction);

        await _handler.Handle(Notification(auction.Id), CancellationToken.None);

        await _notificationService.Received(1).NotifyAuctionCancelledAsync(
            SellerId, auction.Id, "Vase", Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AuctionNotFound_ShouldNotNotifyAndNotThrow()
    {
        var auctionId = Guid.NewGuid();
        _auctionRepository.GetByIdAsync(auctionId, Arg.Any<CancellationToken>()).Returns((Auction?)null);

        await _handler.Handle(Notification(auctionId), CancellationToken.None);

        await _notificationService.DidNotReceiveWithAnyArgs().NotifyAuctionCancelledAsync(
            default, default, default!, default, default);
    }

    [Fact]
    public async Task Handle_NotifierThrows_ShouldNotPropagate()
    {
        var auction = CreateAuction();
        _auctionRepository.GetByIdAsync(auction.Id, Arg.Any<CancellationToken>()).Returns(auction);
        _notificationService
            .NotifyAuctionCancelledAsync(default, default, default!, default, default)
            .ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("boom")));

        var ex = await Record.ExceptionAsync(() => _handler.Handle(Notification(auction.Id), CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Handle_RepositoryThrows_ShouldNotPropagate()
    {
        _auctionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("db down"));

        var ex = await Record.ExceptionAsync(() => _handler.Handle(Notification(Guid.NewGuid()), CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task OutboxPublish_WrappedCancelledEvent_ShouldReachHandler()
    {
        // Guards the outbox bridge: IPublisher.Publish(object) rejects raw domain events.
        var auction = CreateAuction();
        _auctionRepository.GetByIdAsync(auction.Id, Arg.Any<CancellationToken>()).Returns(auction);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_auctionRepository);
        services.AddSingleton(_notificationService);
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<NotifySellerOnAuctionCancelled>());
        await using var provider = services.BuildServiceProvider();
        IDomainEvent domainEvent = new AuctionCancelledEvent(auction.Id, Now);

        await provider.GetRequiredService<IPublisher>().Publish(DomainEventNotification.Wrap(domainEvent));

        await _notificationService.Received(1).NotifyAuctionCancelledAsync(
            SellerId, auction.Id, "Vase", Now, Arg.Any<CancellationToken>());
    }
}
