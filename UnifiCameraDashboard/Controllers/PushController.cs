using Microsoft.AspNetCore.Mvc;
using NotifyHub;
using UnifiCameraDashboard.Services.Notifications;

namespace UnifiCameraDashboard.Controllers;

public record SubscribeRequest(string Endpoint, string P256dh, string Auth);
public record UnsubscribeRequest(string Endpoint);

[ApiController]
[Route("api/[controller]")]
public class PushController : ControllerBase
{
    private readonly VapidKeyProvider _vapidKeyProvider;
    private readonly NotificationSender _notificationSender;
    private readonly IPushSubscriptionRepository _repository;
    private readonly ILogger<PushController> _logger;

    public PushController(
        VapidKeyProvider vapidKeyProvider,
        NotificationSender notificationSender,
        IPushSubscriptionRepository repository,
        ILogger<PushController> logger)
    {
        _vapidKeyProvider = vapidKeyProvider;
        _notificationSender = notificationSender;
        _repository = repository;
        _logger = logger;
    }

    [HttpGet("vapid-public-key")]
    public async Task<IActionResult> GetVapidPublicKey()
    {
        var keys = await _vapidKeyProvider.EnsureKeysAsync();
        return Ok(new { publicKey = keys.PublicKey });
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        await _repository.UpsertAsync(request.Endpoint, request.P256dh, request.Auth);
        return Ok();
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        await _repository.RemoveAsync(request.Endpoint);
        return Ok();
    }

    /// <summary>Sends an immediate test notification to every stored subscription - lets a user verify the whole path (permission, service worker, delivery) without waiting for the daily digest.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> SendTest()
    {
        var subscriptions = await _repository.GetAllAsync();
        if (subscriptions.Count == 0)
        {
            return BadRequest(new { error = "No push subscriptions registered yet." });
        }

        var message = new NotificationMessage
        {
            Title = "UnifiProtectDashboard",
            Body = "Test notification - if you can see this, Web Push is working.",
        };

        var results = await _notificationSender.SendAsync(
            message,
            subscriptions.Select(s => Subscription.WebPush(s.Endpoint, s.P256dh, s.Auth, id: s.Id.ToString())));

        foreach (var (subscription, result) in subscriptions.Zip(results))
        {
            if (result.Outcome == SendOutcome.Expired)
            {
                _logger.LogInformation("Removing expired push subscription {Endpoint}", subscription.Endpoint);
                await _repository.RemoveAsync(subscription.Endpoint);
            }
        }

        var delivered = results.Count(r => r.Outcome == SendOutcome.Delivered);
        return Ok(new { delivered, total = results.Count });
    }
}
