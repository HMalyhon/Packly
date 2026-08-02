using MassTransit;
using Packly.Contracts.Commands;
using Packly.Contracts.Events;

namespace Packly.Payment;

/// <summary>
/// Reverses a payment that was authorised for an order that cannot be fulfilled.
/// </summary>
/// <remarks>
/// The compensating half of <see cref="AuthorizePaymentConsumer"/>, and the reason
/// this workflow is a saga rather than a distributed transaction: the authorisation
/// was committed long ago and cannot be rolled back, so it is undone by a second
/// action that is itself a normal business operation.
/// </remarks>
/// <param name="timeProvider">Clock used to stamp results.</param>
/// <param name="logger">Records each reversal.</param>
public sealed class RefundPaymentConsumer(
    TimeProvider timeProvider,
    ILogger<RefundPaymentConsumer> logger)
    : IConsumer<RefundPayment>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<RefundPayment> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        await Task.Delay(Random.Shared.Next(300, 900), context.CancellationToken);

        logger.LogInformation(
            "Refunded {Amount} against {Reference} for order {OrderId}: {Reason}",
            message.Amount,
            message.PaymentReference,
            message.OrderId,
            message.Reason);

        await context.Publish(
            new PaymentRefunded(
                message.OrderId,
                message.PaymentReference,
                message.Amount,
                timeProvider.GetUtcNow()),
            context.CancellationToken);
    }
}
