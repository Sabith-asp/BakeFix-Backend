using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Repositories;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BakeFix.Controllers
{
    /// <summary>Web Push notification subscriptions and settings. Requires the <b>Notifications</b> module.</summary>
    [ApiController]
    [Route("notifications")]
    [Authorize]
    [RequireModule("Notifications")]
    [Produces("application/json")]
    public class NotificationController : ControllerBase
    {
        private readonly PushNotificationService _pushService;
        private readonly PushSubscriptionRepository _subRepo;
        private readonly NotificationSettingsRepository _settingsRepo;
        private readonly ITenantContext _tenant;

        public NotificationController(
            PushNotificationService pushService,
            PushSubscriptionRepository subRepo,
            NotificationSettingsRepository settingsRepo,
            ITenantContext tenant)
        {
            _pushService   = pushService;
            _subRepo       = subRepo;
            _settingsRepo  = settingsRepo;
            _tenant        = tenant;
        }

        private Guid GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }

        /// <summary>Get the VAPID public key needed to create a browser push subscription.</summary>
        /// <remarks>Pass this key to <c>ServiceWorkerRegistration.pushManager.subscribe()</c> on the client.</remarks>
        [HttpGet("vapid-public-key")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult GetVapidPublicKey()
        {
            return Ok(new { key = _pushService.GetPublicKey() });
        }

        /// <summary>Register a browser push subscription for the current user.</summary>
        /// <param name="request">Push subscription endpoint and keys from the browser PushManager API.</param>
        [HttpPost("subscribe")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionFormData request)
        {
            if (string.IsNullOrWhiteSpace(request.Endpoint))
                return BadRequest(new { message = "Endpoint is required." });

            var sub = new Models.PushSubscription
            {
                Id        = Guid.NewGuid(),
                UserId    = GetUserId(),
                OrgId     = _tenant.RequiredOrgId,
                Endpoint  = request.Endpoint,
                P256dh    = request.P256dh,
                Auth      = request.Auth,
                CreatedAt = DateTime.UtcNow,
            };

            await _subRepo.SaveAsync(sub);
            return Ok(new { message = "Subscribed successfully." });
        }

        /// <summary>Remove a browser push subscription for the current user.</summary>
        /// <param name="request">The subscription endpoint to remove.</param>
        [HttpDelete("subscribe")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
        {
            await _subRepo.DeleteAsync(request.Endpoint, GetUserId());
            return NoContent();
        }

        /// <summary>Get the organisation's notification schedule settings.</summary>
        [HttpGet("settings")]
        [ProducesResponseType(typeof(NotificationSettings), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSettings()
        {
            return Ok(await _settingsRepo.GetAsync());
        }

        /// <summary>Update the organisation's notification schedule settings.</summary>
        /// <remarks><c>ReminderHour</c> must be between 0 and 23 (24-hour clock).</remarks>
        /// <param name="request">Notification settings.</param>
        [HttpPut("settings")]
        [ProducesResponseType(typeof(NotificationSettings), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateSettings([FromBody] NotificationSettingsFormData request)
        {
            if (request.ReminderHour < 0 || request.ReminderHour > 23)
                return BadRequest(new { message = "ReminderHour must be between 0 and 23." });

            var settings = new NotificationSettings
            {
                OrgId                = _tenant.RequiredOrgId,
                DailyReminderEnabled = request.DailyReminderEnabled,
                ReminderHour         = request.ReminderHour,
                WeeklySummaryEnabled = request.WeeklySummaryEnabled,
                BudgetAlertsEnabled  = request.BudgetAlertsEnabled,
            };

            await _settingsRepo.UpsertAsync(settings);
            return Ok(settings);
        }

        /// <summary>Send a test push notification to all subscribed devices in the organisation.</summary>
        [HttpPost("test")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SendTest()
        {
            await _pushService.SendToOrgAsync(
                _tenant.RequiredOrgId,
                "Fynlo Test",
                "Push notifications are working! 🎉",
                "/");

            return Ok(new { message = "Test notification sent." });
        }
    }
}
