namespace BakeFix.Models
{
    public class ProductCategory
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class ProductCategoryFormData
    {
        public string Name { get; set; } = "";
    }
}
