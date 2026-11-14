using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuctionKings.Application.Events;

/// <summary>Tells the seller (and only the seller) that their auction was cancelled.</summary>
public sealed partial class NotifySellerOnAuctionCancelled
    : INotificationHandler<DomainEventNotification<AuctionCancelledEvent>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IRealtimeNotificationService _notificationService;
    private readonly ILogger<NotifySellerOnAuctionCancelled> _logger;

    public NotifySellerOnAuctionCancelled(
        IAuctionRepository auctionRepository,
        IRealtimeNotificationService notificationService,
        ILogger<NotifySellerOnAuctionCancelled> logger)
    {
        _auctionRepository = auctionRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<AuctionCancelledEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        try
        {
            var auction = await _auctionRepository.GetByIdAsync(domainEvent.AuctionId, cancellationToken);

            if (auction is null)
            {
                LogAuctionNotFound(_logger, domainEvent.AuctionId);
                return;
            }

            await _notificationService.NotifyAuctionCancelledAsync(
                auction.SellerId,
                auction.Id,
                auction.Title,
                domainEvent.CancelledAt,
                CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort push: a failure must not fail the outbox message (a retry would
            // re-notify and hold back the SQS publish).
            LogNotifyFailed(_logger, domainEvent.AuctionId, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AuctionCancelledEvent for unknown auction {AuctionId}; seller not notified.")]
    private static partial void LogAuctionNotFound(ILogger logger, Guid auctionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seller notification for cancelled auction {AuctionId} failed; continuing.")]
    private static partial void LogNotifyFailed(ILogger logger, Guid auctionId, Exception ex);
}
