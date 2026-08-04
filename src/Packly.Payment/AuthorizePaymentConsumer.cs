using MassTransit;
using Packly.Contracts.Commands;
using Packly.Contracts.Events;

namespace Packly.Payment;

/// <summary>
/// Authorises payment for an order, or refuses it.
/// </summary>
/// <remarks>
/// <para>
/// A stand-in for a payment gateway. It knows how to do one thing and nothing
/// about what happens before or after: the orchestrator decides when to ask and
/// what to do with the answer, which is what keeps this service replaceable.
/// </para>
/// <para>
/// Both outcomes are ordinary results, not failures. A declined card is a normal
/// business answer and is reported as an event; throwing would make the workflow's
/// most predictable branch look like a bug and send the message to the error queue
/// instead of to the saga.
/// </para>
/// </remarks>
/// <param name="logger">Records each decision.</param>
public sealed class AuthorizePaymentConsumer(ILogger<AuthorizePaymentConsumer> logger)
    : IConsumer<AuthorizePayment>
{
    // Deterministic on purpose: a random failure rate would make the declined
    // path something you wait for rather than something you can trigger.
    private const decimal DeclineThreshold = 1000m;

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AuthorizePayment> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        // Stands in for the round trip to a real gateway. Without it the whole
        // flow finishes faster than anything watching can resolve, which makes
        // asynchronous steps look synchronous.
        await Task.Delay(Random.Shared.Next(300, 900), context.CancellationToken);

        if (message.Amount >= DeclineThreshold)
        {
            logger.LogInformation(
                "Payment declined for order {OrderId}: {Amount} is at or above the {Threshold} limit",
                message.OrderId,
                message.Amount,
                DeclineThreshold);

            await context.Publish(
                new PaymentDeclined(
                    message.OrderId,
                    $"Amount {message.Amount:0.00} exceeds the authorization limit."),
                context.CancellationToken);

            return;
        }

        // Version 4, not 7. Only the leading characters survive the truncation,
        // and those are exactly where a version 7 GUID keeps its millisecond
        // timestamp: every authorisation in the same millisecond would share a
        // reference. This is the handle a refund is issued against.
        var reference = $"PAY-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

        logger.LogInformation(
            "Payment authorized for order {OrderId}: {Amount}, reference {Reference}",
            message.OrderId,
            message.Amount,
            reference);

        await context.Publish(
            new PaymentAuthorized(
                message.OrderId,
                reference,
                message.Amount),
            context.CancellationToken);
    }
}
