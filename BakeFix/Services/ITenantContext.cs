namespace BakeFix.Services
{
    public interface ITenantContext
    {
        Guid? OrganizationId { get; }
        Guid? UserId { get; }
        string Username { get; }
        string Role { get; }
        bool IsSuperAdmin { get; }
        Guid RequiredOrgId { get; }
        Guid RequiredUserId { get; }
        string OrgTimezone { get; }
        DateTime OrgLocalNow { get; }
        DateTime OrgLocalDate { get; }
    }
}
