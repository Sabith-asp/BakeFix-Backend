using BakeFix.DTOs;
using BakeFix.Models;
using BakeFix.Repositories;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("admin")]
    [Authorize]
    public class SuperAdminController : ControllerBase
    {
        private readonly IOrganizationRepository _orgRepo;
        private readonly UserRepository _userRepo;
        private readonly ITenantContext _tenant;

        public SuperAdminController(IOrganizationRepository orgRepo, UserRepository userRepo, ITenantContext tenant)
        {
            _orgRepo = orgRepo;
            _userRepo = userRepo;
            _tenant = tenant;
        }

        private IActionResult? DeniedIfNotSuperAdmin()
        {
            if (!_tenant.IsSuperAdmin)
                return StatusCode(403, new { message = "SuperAdmin access required." });
            return null;
        }

        // ── Organizations ────────────────────────────────────────────────────

        // GET /admin/organizations
        [HttpGet("organizations")]
        public async Task<IActionResult> ListOrganizations()
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            return Ok(await _orgRepo.GetAllAsync());
        }

        // GET /admin/organizations/{id}
        [HttpGet("organizations/{id:guid}")]
        public async Task<IActionResult> GetOrganization(Guid id)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            var org = await _orgRepo.GetByIdAsync(id);
            if (org is null) return NotFound(new { message = "Organization not found." });

            return Ok(org);
        }

        // POST /admin/organizations
        [HttpPost("organizations")]
        public async Task<IActionResult> CreateOrganization([FromBody] CreateOrgRequest request)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            var org = await _orgRepo.CreateAsync(new Organization
            {
                Name = request.Name,
                Slug = request.Slug
            });

            return Ok(org);
        }

        // PUT /admin/organizations/{id}/modules/{moduleName}
        [HttpPut("organizations/{id:guid}/modules/{moduleName}")]
        public async Task<IActionResult> ToggleModule(Guid id, string moduleName, [FromBody] ToggleModuleRequest request)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            await _orgRepo.SetModuleEnabledAsync(id, moduleName, request.Enabled);
            return Ok(new { message = $"Module '{moduleName}' {(request.Enabled ? "enabled" : "disabled")} successfully." });
        }

        // PATCH /admin/organizations/{id}/status
        [HttpPatch("organizations/{id:guid}/status")]
        public async Task<IActionResult> SetOrgStatus(Guid id, [FromBody] SetOrgStatusRequest request)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            await _orgRepo.SetActiveAsync(id, request.IsActive);
            return Ok(new { message = $"Organization {(request.IsActive ? "activated" : "suspended")} successfully." });
        }

        // ── Users ────────────────────────────────────────────────────────────

        // GET /admin/organizations/{id}/users
        [HttpGet("organizations/{id:guid}/users")]
        public async Task<IActionResult> GetOrgUsers(Guid id)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            return Ok(await _userRepo.GetUsersByOrgAsync(id));
        }

        // POST /admin/organizations/{id}/users
        [HttpPost("organizations/{id:guid}/users")]
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

        // DELETE /admin/organizations/{id}/users/{userId}
        [HttpDelete("organizations/{id:guid}/users/{userId:guid}")]
        public async Task<IActionResult> DeleteOrgUser(Guid id, Guid userId)
        {
            var deny = DeniedIfNotSuperAdmin();
            if (deny is not null) return deny;

            await _userRepo.DeleteUserAsync(userId);
            return NoContent();
        }
    }
}
