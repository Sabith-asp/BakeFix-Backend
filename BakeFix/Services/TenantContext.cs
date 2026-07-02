using Dapper;
using MySql.Data.MySqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BakeFix.Services
{
    public class TenantContext : ITenantContext
    {
        private readonly string _connStr;
        private string? _orgTimezone;

        public Guid? OrganizationId { get; }
        public Guid? UserId { get; }
        public string Username { get; }
        public string Role { get; }
        public bool IsSuperAdmin => Role == "SuperAdmin";

        public Guid RequiredOrgId =>
            OrganizationId ?? throw new UnauthorizedAccessException("No organization context.");

        public Guid RequiredUserId =>
            UserId ?? throw new UnauthorizedAccessException("No user context.");

        public string OrgTimezone
        {
            get
            {
                if (_orgTimezone is not null) return _orgTimezone;
                if (OrganizationId is null) return (_orgTimezone = "UTC");
                using var conn = new MySqlConnection(_connStr);
                _orgTimezone = conn.QueryFirstOrDefault<string>(
                    "SELECT Timezone FROM Organizations WHERE Id = @id",
                    new { id = OrganizationId }) ?? "UTC";
                return _orgTimezone;
            }
        }

        public DateTime OrgLocalNow =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetTzInfo());

        public DateTime OrgLocalDate => OrgLocalNow.Date;

        private TimeZoneInfo GetTzInfo()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(OrgTimezone); }
            catch { return TimeZoneInfo.Utc; }
        }

        public TenantContext(IHttpContextAccessor accessor, IConfiguration config)
        {
            _connStr = config.GetConnectionString("DefaultConnection")!;

            var user = accessor.HttpContext?.User;
            Role     = user?.FindFirstValue(ClaimTypes.Role) ?? "";

            var rawOrg = user?.FindFirstValue("organizationId");
            OrganizationId = Guid.TryParse(rawOrg, out var oid) ? oid : null;

            var rawUser = user?.FindFirstValue(ClaimTypes.NameIdentifier);
            UserId = Guid.TryParse(rawUser, out var uid) ? uid : null;

            Username = user?.FindFirstValue(ClaimTypes.Name) ?? "";
        }
    }
}
