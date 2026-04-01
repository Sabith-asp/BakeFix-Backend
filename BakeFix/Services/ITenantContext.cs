namespace BakeFix.Services
{
    public interface ITenantContext
    {
        Guid? OrganizationId { get; }
        string Role { get; }
        bool IsSuperAdmin { get; }

        /// <summary>
        /// Returns OrganizationId or throws UnauthorizedAccessException if no org is set.
        /// Use this in all repository queries that must be tenant-scoped.
        /// </summary>
        Guid RequiredOrgId { get; }
    }
}
