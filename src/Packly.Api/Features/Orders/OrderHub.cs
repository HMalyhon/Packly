using Microsoft.AspNetCore.SignalR;

namespace Packly.Api.Features.Orders;

/// <summary>
/// The connection a browser holds open to follow one order.
/// </summary>
/// <remarks>
/// Clients receive nothing by default. A page asks to watch an order and is put
/// in a group named after it, so a status change is delivered to the people
/// looking at that order rather than broadcast to everyone connected.
/// </remarks>
public sealed class OrderHub : Hub
{
    /// <summary>The path the hub is served from.</summary>
    public const string Path = "/hubs/orders";

    /// <summary>
    /// Subscribes the calling connection to one order's updates.
    /// </summary>
    /// <param name="orderId">The order to follow.</param>
    /// <returns>A task that completes once the connection has joined.</returns>
    public Task Watch(Guid orderId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(orderId));

    /// <summary>
    /// Stops the calling connection following an order.
    /// </summary>
    /// <param name="orderId">The order to stop following.</param>
    /// <returns>A task that completes once the connection has left.</returns>
    public Task Unwatch(Guid orderId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(orderId));

    /// <summary>
    /// Names the group carrying one order's updates.
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <returns>The group name.</returns>
    internal static string GroupFor(Guid orderId) => $"order-{orderId}";
}
