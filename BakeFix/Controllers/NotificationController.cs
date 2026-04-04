using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Repositories;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("notifications")]
    [Authorize]
    [RequireModule("Notifications")]
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

        // GET /notifications/vapid-public-key
        [HttpGet("vapid-public-key")]
        public IActionResult GetVapidPublicKey()
        {
            return Ok(new { key = _pushService.GetPublicKey() });
        }

        // POST /notifications/subscribe
        [HttpPost("subscribe")]
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

        // DELETE /notifications/subscribe
        [HttpDelete("subscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
        {
            await _subRepo.DeleteAsync(request.Endpoint, GetUserId());
            return NoContent();
        }

        // GET /notifications/settings
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            return Ok(await _settingsRepo.GetAsync());
        }

        // PUT /notifications/settings
        [HttpPut("settings")]
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

        // POST /notifications/test
        [HttpPost("test")]
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
