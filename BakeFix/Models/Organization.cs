namespace BakeFix.Models
{
    public class Organization
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> EnabledModules { get; set; } = new();
    }
}
