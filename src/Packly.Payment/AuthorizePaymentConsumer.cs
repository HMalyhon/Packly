using System.Security.Cryptography;
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

            // "At or above", not "exceeds": the rule is >=, so at exactly the
            // threshold the older wording told the customer their total exceeded a
            // number it equalled.
            var reason = $"Amount {message.Amount:0.00} is at or above the " +
                $"{DeclineThreshold:0.00} authorization limit.";

            await context.Publish(
                new PaymentDeclined(message.OrderId, reason),
                context.CancellationToken);

            return;
        }

        // Derived from the order rather than minted per delivery. This is the handle
        // a refund is issued against, and delivery is at-least-once: a worker that
        // crashes between publishing and acknowledging runs this again, and a fresh
        // GUID each time would authorise against one reference while the saga - which
        // keeps the first and ignores the second - refunds against another. Against a
        // real gateway that is a hold nobody reverses.
        //
        // Hashed rather than sliced off the order id, because that id is a version 7
        // GUID: its leading characters are a millisecond timestamp, so truncating it
        // would give every order placed in the same millisecond the same reference.
        var reference = $"PAY-{Convert.ToHexString(SHA256.HashData(message.OrderId.ToByteArray()))[..12]}";

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
