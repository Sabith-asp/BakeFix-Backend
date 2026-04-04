namespace BakeFix.Models
{
    public class NotificationSettings
    {
        public Guid OrgId { get; set; }
        public bool DailyReminderEnabled { get; set; } = true;
        public int ReminderHour { get; set; } = 20;
        public bool WeeklySummaryEnabled { get; set; } = false;
        public bool BudgetAlertsEnabled { get; set; } = true;
    }

    public class NotificationSettingsFormData
    {
        public bool DailyReminderEnabled { get; set; }
        public int ReminderHour { get; set; }
        public bool WeeklySummaryEnabled { get; set; }
        public bool BudgetAlertsEnabled { get; set; }
    }
}
