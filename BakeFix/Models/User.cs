namespace BakeFix.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public Guid? OrganizationId { get; set; }
        public int RoleId { get; set; }
        public string Role { get; set; } = "";
        public string? OrganizationName { get; set; }
        public bool? OrgIsActive { get; set; }
    }
}
