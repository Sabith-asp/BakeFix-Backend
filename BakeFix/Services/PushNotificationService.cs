using BakeFix.Repositories;
using System.Text.Json;
using WebPush;

namespace BakeFix.Services
{
    public class PushNotificationService
    {
        private readonly PushSubscriptionRepository _subRepo;
        private readonly IConfiguration _config;
        private readonly ILogger<PushNotificationService> _logger;

        public PushNotificationService(
            PushSubscriptionRepository subRepo,
            IConfiguration config,
            ILogger<PushNotificationService> logger)
        {
            _subRepo = subRepo;
            _config = config;
            _logger = logger;
        }

        public string GetPublicKey() => _config["Vapid:PublicKey"] ?? "";

        public async Task SendToOrgAsync(Guid orgId, string title, string body, string url = "/")
        {
            var subscriptions = await _subRepo.GetByOrgIdAsync(orgId);
            var payload = JsonSerializer.Serialize(new { title, body, url });
            await SendAsync(subscriptions, payload);
        }

        public async Task SendToSubscriptionAsync(Models.PushSubscription sub, string title, string body, string url = "/")
        {
            var payload = JsonSerializer.Serialize(new { title, body, url });
            await SendAsync(new[] { sub }, payload);
        }

        private async Task SendAsync(IEnumerable<Models.PushSubscription> subscriptions, string payload)
        {
            var subject    = _config["Vapid:Subject"]    ?? "";
            var publicKey  = _config["Vapid:PublicKey"]  ?? "";
            var privateKey = _config["Vapid:PrivateKey"] ?? "";

            if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
            {
                _logger.LogWarning("VAPID keys not configured — push notifications disabled.");
                return;
            }

            var vapidDetails = new VapidDetails(subject, publicKey, privateKey);
            var client = new WebPushClient();

            foreach (var sub in subscriptions)
            {
                try
                {
                    var pushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await client.SendNotificationAsync(pushSub, payload, vapidDetails);
                }
                catch (WebPushException ex) when ((int)ex.StatusCode == 410 || (int)ex.StatusCode == 404)
                {
                    await _subRepo.DeleteStaleAsync(sub.Endpoint);
                    _logger.LogInformation("Removed stale push subscription: {Endpoint}", sub.Endpoint[..Math.Min(40, sub.Endpoint.Length)]);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send push notification.");
                }
            }
        }
    }
}
