using System.Security.Claims;

namespace BakeFix.Services
{
    public class TenantContext : ITenantContext
    {
        public Guid? OrganizationId { get; }
        public string Role { get; }
        public bool IsSuperAdmin => Role == "SuperAdmin";
        public Guid RequiredOrgId =>
            OrganizationId ?? throw new UnauthorizedAccessException("No organization context.");

        public TenantContext(IHttpContextAccessor accessor)
        {
            var user = accessor.HttpContext?.User;
            Role = user?.FindFirstValue(ClaimTypes.Role) ?? "";
            var raw = user?.FindFirstValue("organizationId");
            OrganizationId = Guid.TryParse(raw, out var id) ? id : null;
        }
    }
}
