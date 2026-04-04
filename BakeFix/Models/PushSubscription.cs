namespace BakeFix.Models
{
    public class PushSubscription
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid OrgId { get; set; }
        public string Endpoint { get; set; } = "";
        public string P256dh { get; set; } = "";
        public string Auth { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class PushSubscriptionFormData
    {
        public string Endpoint { get; set; } = "";
        public string P256dh { get; set; } = "";
        public string Auth { get; set; } = "";
    }

    public class UnsubscribeRequest
    {
        public string Endpoint { get; set; } = "";
    }
}
