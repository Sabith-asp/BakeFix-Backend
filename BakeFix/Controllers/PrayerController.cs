using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("prayer")]
    [Authorize]
    [RequireModule("Prayer")]
    public class PrayerController : ControllerBase
    {
        private readonly PrayerService _prayer;

        public PrayerController(PrayerService prayer)
        {
            _prayer = prayer;
        }

        // ── Dashboard & Today ────────────────────────────────────────────────

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var result = await _prayer.GetDashboardAsync();
            return Ok(result);
        }

        // ── History ──────────────────────────────────────────────────────────

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] string? startDate,
            [FromQuery] string? endDate)
        {
            var from = startDate is not null && DateTime.TryParse(startDate, out var f)
                ? f : DateTime.UtcNow.Date.AddDays(-30);
            var to = endDate is not null && DateTime.TryParse(endDate, out var t)
                ? t : DateTime.UtcNow.Date;

            var result = await _prayer.GetHistoryAsync(from, to);
            return Ok(result);
        }

        // ── Status Update (one-tap) ──────────────────────────────────────────

        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdatePrayerStatusRequest request)
        {
            var updated = await _prayer.UpdateStatusAsync(id, request);
            return Ok(updated);
        }

        // ── Streak ───────────────────────────────────────────────────────────

        [HttpGet("streak")]
        public async Task<IActionResult> GetStreak()
        {
            var streak = await _prayer.GetStreakAsync();
            return Ok(streak);
        }

        // ── Reminder Configs ─────────────────────────────────────────────────

        [HttpGet("reminders")]
        public async Task<IActionResult> GetReminders()
        {
            var configs = await _prayer.GetReminderConfigsAsync();
            return Ok(configs);
        }

        [HttpPut("reminders")]
        public async Task<IActionResult> UpdateReminder([FromBody] UpdateReminderConfigRequest request)
        {
            await _prayer.UpdateReminderConfigAsync(request);
            return NoContent();
        }

        // ── Org Settings (admin) ─────────────────────────────────────────────

        [HttpGet("org-settings")]
        public async Task<IActionResult> GetOrgSettings()
        {
            var settings = await _prayer.GetOrgSettingsAsync();
            return Ok(settings);
        }

        [HttpPut("org-settings")]
        public async Task<IActionResult> UpdateOrgSettings([FromBody] UpdateOrgSettingsRequest request)
        {
            await _prayer.UpdateOrgSettingsAsync(request);
            return NoContent();
        }

        // ── User Location ────────────────────────────────────────────────────

        [HttpGet("user-location")]
        public async Task<IActionResult> GetUserLocation()
        {
            var settings = await _prayer.GetUserSettingsAsync();
            return Ok(settings);
        }

        [HttpPut("user-location")]
        public async Task<IActionResult> UpdateUserLocation([FromBody] UpdateUserLocationRequest request)
        {
            await _prayer.UpdateUserLocationAsync(request);
            return NoContent();
        }

        [HttpDelete("user-location")]
        public async Task<IActionResult> ClearUserLocation()
        {
            await _prayer.ClearUserLocationAsync();
            return NoContent();
        }

        // ── Admin Summary ────────────────────────────────────────────────────

        [HttpGet("admin/summary")]
        public async Task<IActionResult> GetAdminSummary([FromQuery] string? date)
        {
            DateTime? parsed = date is not null && DateTime.TryParse(date, out var d) ? d : null;
            var summary = await _prayer.GetAdminSummaryAsync(parsed);
            return Ok(summary);
        }
    }
}
