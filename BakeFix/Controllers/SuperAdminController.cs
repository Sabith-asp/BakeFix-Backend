using BakeFix.DTOs;
using BakeFix.Models;
using BakeFix.Repositories;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    /// <summary>SuperAdmin-only endpoints for managing organisations and their users.</summary>
    /// <remarks>
    /// All endpoints in this controller require the caller to have the <b>SuperAdmin</b> role.
    /// Org-scoped users will receive <c>403 Forbidden</c>.
    /// </remarks>
    [ApiController]
    [Route("admin")]
    [Authorize]
    [Produces("application/json")]
    public class SuperAdminController : ControllerBase
    {
        private readonly IOrganizationRepository _orgRepo;
        private readonly UserRepository _userRepo;
        private readonly DivisionRepository _divisionRepo;
        private readonly PushSubscriptionRepository _subRepo;
        private readonly NotificationSettingsRepository _notifSettingsRepo;
        private readonly ITenantContext _tenant;

        public SuperAdminController(
            IOrganizationRepository orgRepo,
            UserRepository userRepo,
            DivisionRepository divisionRepo,
            PushSubscriptionRepository subRepo,
            NotificationSettingsRepository notifSettingsRepo,
            ITenantContext tenant)
        {
            _orgRepo           = orgRepo;
            _userRepo          = userRepo;
            _divisionRepo      = divisionRepo;
            _subRepo           = subRepo;
            _notifSettingsRepo = notifSettingsRepo;
            _tenant            = tenant;
        }

        private IActionResult? DeniedIfNotSuperAdmin()
        {
            if (!_tenant.IsSuperAdmin)
                return StatusCode(403, new { message = "SuperAdmin access required." });
            return null;
        }

        // ── Organizations ────────────────────────────────────────────────────

        /// <summary>List all organisations.</summary>
        [HttpGet("organizations")]
        [ProducesResponseType(typeof(IEnumerable<Organization>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ListOrganizations()
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            return Ok(await _orgRepo.GetAllAsync());
        }

        /// <summary>Get a single organisation by ID, including its enabled module list.</summary>
        /// <param name="id">Organisation ID.</param>
        [HttpGet("organizations/{id:guid}")]
        [ProducesResponseType(typeof(Organization), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrganization(Guid id)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            var org = await _orgRepo.GetByIdAsync(id);
            if (org is null) return NotFound(new { message = "Organization not found." });

            return Ok(org);
        }

        /// <summary>Create a new organisation.</summary>
        /// <remarks>
        /// The <c>slug</c> is a lowercase URL-safe identifier (e.g. <c>sunrise-bakery</c>).
        /// All modules are created in a disabled state; enable them individually after creation.
        /// </remarks>
        /// <param name="request">Organisation name and slug.</param>
        [HttpPost("organizations")]
        [ProducesResponseType(typeof(Organization), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateOrganization([FromBody] CreateOrgRequest request)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            if (string.IsNullOrWhiteSpace(request.Timezone))
                return BadRequest(new { message = "Timezone is required." });

            var org = await _orgRepo.CreateAsync(new Organization
            {
                Name     = request.Name,
                Slug     = request.Slug,
                Timezone = request.Timezone,
            });

            return Ok(org);
        }

        /// <summary>Enable or disable a feature module for an organisation.</summary>
        /// <remarks>
        /// Valid module names: <c>Income</c>, <c>Expenses</c>, <c>Wages</c>, <c>Employees</c>,
        /// <c>Divisions</c>, <c>Debts</c>, <c>Inventory</c>, <c>Notifications</c>.
        /// </remarks>
        /// <param name="id">Organisation ID.</param>
        /// <param name="moduleName">Module name (case-sensitive).</param>
        /// <param name="request">Desired enabled state.</param>
        [HttpPut("organizations/{id:guid}/modules/{moduleName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ToggleModule(Guid id, string moduleName, [FromBody] ToggleModuleRequest request)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            await _orgRepo.SetModuleEnabledAsync(id, moduleName, request.Enabled);
            return Ok(new { message = $"Module '{moduleName}' {(request.Enabled ? "enabled" : "disabled")} successfully." });
        }

        /// <summary>Update the timezone for an organisation.</summary>
        [HttpPatch("organizations/{id:guid}/timezone")]
        public async Task<IActionResult> UpdateTimezone(Guid id, [FromBody] UpdateTimezoneRequest request)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            if (string.IsNullOrWhiteSpace(request.Timezone))
                return BadRequest(new { message = "Timezone is required." });

            await _orgRepo.UpdateTimezoneAsync(id, request.Timezone);
            return Ok(new { message = "Timezone updated." });
        }

        /// <summary>Activate or suspend an organisation.</summary>
        /// <remarks>Suspended organisations cannot log in.</remarks>
        /// <param name="id">Organisation ID.</param>
        /// <param name="request">Desired active state.</param>
        [HttpPatch("organizations/{id:guid}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SetOrgStatus(Guid id, [FromBody] SetOrgStatusRequest request)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            await _orgRepo.SetActiveAsync(id, request.IsActive);
            return Ok(new { message = $"Organization {(request.IsActive ? "activated" : "suspended")} successfully." });
        }

        // ── Users ────────────────────────────────────────────────────────────

        /// <summary>List all users belonging to an organisation.</summary>
        /// <param name="id">Organisation ID.</param>
        [HttpGet("organizations/{id:guid}/users")]
        [ProducesResponseType(typeof(IEnumerable<OrgUserResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOrgUsers(Guid id)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            return Ok(await _userRepo.GetUsersByOrgAsync(id));
        }

        /// <summary>Create a new user and assign them to an organisation.</summary>
        /// <remarks>
        /// <c>RoleId</c>: <c>2</c> = OrgAdmin, <c>3</c> = Member (default).
        /// Usernames must be unique across all organisations.
        /// </remarks>
        /// <param name="id">Organisation ID.</param>
        /// <param name="request">Username, password, and role.</param>
        [HttpPost("organizations/{id:guid}/users")]
        [ProducesResponseType(typeof(OrgUserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateOrgUser(Guid id, [FromBody] CreateUserRequest request)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(new { message = "Username is required." });

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Password is required." });

            var exists = await _userRepo.UsernameExistsAsync(request.Username.Trim());
            if (exists)
                return Conflict(new { message = "Username already exists." });

            var org = await _orgRepo.GetByIdAsync(id);
            if (org is null)
                return NotFound(new { message = "Organization not found." });

            var user = new User
            {
                Id             = Guid.NewGuid(),
                Username       = request.Username.Trim(),
                Password       = request.Password,
                PasswordHash   = BCrypt.Net.BCrypt.HashPassword(request.Password),
                OrganizationId = id,
                RoleId         = request.RoleId
            };

            await _userRepo.CreateUserAsync(user);

            return Ok(new OrgUserResponse
            {
                Id       = user.Id,
                Username = user.Username,
                Role     = request.RoleId == 2 ? "OrgAdmin" : "Member"
            });
        }

        /// <summary>Delete a user from an organisation.</summary>
        /// <param name="id">Organisation ID.</param>
        /// <param name="userId">User ID to delete.</param>
        [HttpDelete("organizations/{id:guid}/users/{userId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteOrgUser(Guid id, Guid userId)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            await _userRepo.DeleteUserAsync(userId);
            return NoContent();
        }

        // ── Supporting data ──────────────────────────────────────────────────

        /// <summary>List divisions configured for an organisation.</summary>
        /// <param name="id">Organisation ID.</param>
        [HttpGet("organizations/{id:guid}/divisions")]
        [ProducesResponseType(typeof(IEnumerable<Division>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOrgDivisions(Guid id)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            return Ok(await _divisionRepo.GetByOrgIdAsync(id));
        }

        /// <summary>Get push notification status for an organisation.</summary>
        /// <remarks>Returns the active subscription count and the current notification settings.</remarks>
        /// <param name="id">Organisation ID.</param>
        [HttpGet("organizations/{id:guid}/notifications")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOrgNotifications(Guid id)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            var subscriptionCount = await _subRepo.GetCountByOrgIdAsync(id);
            var settings          = await _notifSettingsRepo.GetByOrgIdAsync(id);

            return Ok(new
            {
                subscriptionCount,
                settings
            });
        }
    }
}
