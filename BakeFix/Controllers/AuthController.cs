using BakeFix.DTOs;
using BakeFix.Repositories;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    /// <summary>Authentication endpoints — no JWT required.</summary>
    [ApiController]
    [Route("auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _auth;
        private readonly ITenantContext _tenant;
        private readonly UserRepository _userRepo;
        private readonly IOrganizationRepository _orgRepo;

        public AuthController(AuthService auth, ITenantContext tenant, UserRepository userRepo, IOrganizationRepository orgRepo)
        {
            _auth = auth;
            _tenant = tenant;
            _userRepo = userRepo;
            _orgRepo = orgRepo;
        }

        /// <summary>Authenticate a user and obtain a JWT token.</summary>
        /// <remarks>
        /// Returns user profile, JWT Bearer token, and the list of modules enabled for
        /// the user's organisation. SuperAdmin users have <c>OrganizationId = null</c>.
        /// </remarks>
        /// <param name="request">Username and password.</param>
        /// <returns>JWT token plus user/org metadata.</returns>
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _auth.LoginAsync(request.Username, request.Password);

            if (!result.Success)
                return Unauthorized(new { message = result.ErrorMessage });

            var response = new LoginResponse
            {
                Id                    = result.User!.Id,
                Username              = result.User.Username,
                Token                 = result.Token!,
                Role                  = result.User.Role,
                OrganizationId        = result.User.OrganizationId,
                OrganizationName      = result.OrgName,
                EnabledModules        = result.Modules,
                OrganizationTimezone  = result.Timezone,
            };

            return Ok(response);
        }

        /// <summary>Returns fresh user profile + enabled modules for the currently authenticated user.</summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var user = await _userRepo.GetUserByIdAsync(_tenant.RequiredUserId);
            if (user == null) return Unauthorized();

            var modules  = new List<string>();
            var timezone = "";
            if (user.OrganizationId.HasValue)
            {
                modules  = await _orgRepo.GetEnabledModulesAsync(user.OrganizationId.Value);
                var org  = await _orgRepo.GetByIdAsync(user.OrganizationId.Value);
                timezone = org?.Timezone ?? "Asia/Kolkata";
            }

            return Ok(new LoginResponse
            {
                Id                   = user.Id,
                Username             = user.Username,
                Token                = Request.Headers.Authorization.ToString().Replace("Bearer ", ""),
                Role                 = user.Role,
                OrganizationId       = user.OrganizationId,
                OrganizationName     = user.OrganizationName ?? "",
                EnabledModules       = modules,
                OrganizationTimezone = timezone,
            });
        }
    }
}
