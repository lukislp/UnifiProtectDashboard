using Microsoft.EntityFrameworkCore;
using UnifiCameraDashboard.Data;

namespace UnifiCameraDashboard.Services.Notifications;

public interface IPushSubscriptionRepository
{
    /// <summary>Adds a new subscription, or updates the keys if the same endpoint already exists (a browser can re-subscribe with fresh keys under the same endpoint).</summary>
    Task UpsertAsync(string endpoint, string p256dh, string auth);
    Task RemoveAsync(string endpoint);
    Task<List<StoredPushSubscription>> GetAllAsync();
}

public class PushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly DashboardDbContext _context;
    private readonly ILogger<PushSubscriptionRepository> _logger;

    public PushSubscriptionRepository(DashboardDbContext context, ILogger<PushSubscriptionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task UpsertAsync(string endpoint, string p256dh, string auth)
    {
        try
        {
            var existing = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
            if (existing != null)
            {
                existing.P256dh = p256dh;
                existing.Auth = auth;
            }
            else
            {
                _context.PushSubscriptions.Add(new StoredPushSubscription
                {
                    Endpoint = endpoint,
                    P256dh = p256dh,
                    Auth = auth,
                });
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving push subscription");
            throw;
        }
    }

    public async Task RemoveAsync(string endpoint)
    {
        try
        {
            var existing = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
            if (existing == null)
            {
                return;
            }

            _context.PushSubscriptions.Remove(existing);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing push subscription");
            throw;
        }
    }

    public async Task<List<StoredPushSubscription>> GetAllAsync()
    {
        try
        {
            return await _context.PushSubscriptions.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving push subscriptions");
            return new List<StoredPushSubscription>();
        }
    }
}
