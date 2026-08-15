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
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AuthorizePayment> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        // Stands in for the round trip to a real gateway. Without it the whole
        // flow finishes faster than anything watching can resolve, which makes
        // asynchronous steps look synchronous.
        await Task.Delay(Random.Shared.Next(300, 900), context.CancellationToken);

        if (AuthorizationRule.IsDeclined(message.Amount))
        {
            logger.LogInformation(
                "Payment declined for order {OrderId}: {Amount} is at or above the {Threshold} limit",
                message.OrderId,
                message.Amount,
                AuthorizationRule.DeclineThreshold);

            await context.Publish(
                new PaymentDeclined(
                    message.OrderId,
                    AuthorizationRule.DeclineReason(message.Amount)),
                context.CancellationToken);

            return;
        }

        var reference = AuthorizationRule.ReferenceFor(message.OrderId);

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
