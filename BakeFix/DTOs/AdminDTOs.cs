namespace BakeFix.DTOs
{
    public class CreateOrgRequest
    {
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
    }

    public class ToggleModuleRequest
    {
        public bool Enabled { get; set; }
    }

    public class SetOrgStatusRequest
    {
        public bool IsActive { get; set; }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public int RoleId { get; set; } = 3;  // 2 = OrgAdmin, 3 = Member
    }

    public class OrgUserResponse
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
    }
}
