namespace BakeFix.Models
{
    public static class PrayerStatus
    {
        public const string Upcoming = "Upcoming";
        public const string ReminderSent = "ReminderSent";
        public const string Pending = "Pending";
        public const string CompletedOnTime = "CompletedOnTime";
        public const string CompletedLate = "CompletedLate";
        public const string Missed = "Missed";
        public const string Excused = "Excused";
        public const string QadaCompleted = "QadaCompleted";
        public const string Skipped = "Skipped";

        public static readonly string[] ValidUserSetStatuses =
            { CompletedOnTime, CompletedLate, Missed, Excused, QadaCompleted, Skipped };

        public static readonly string[] CompletedStatuses =
            { CompletedOnTime, CompletedLate, QadaCompleted };
    }

    public static class PrayerNames
    {
        public const string Fajr = "Fajr";
        public const string Dhuhr = "Dhuhr";
        public const string Asr = "Asr";
        public const string Maghrib = "Maghrib";
        public const string Isha = "Isha";

        public static readonly string[] All = { Fajr, Dhuhr, Asr, Maghrib, Isha };
    }

    public class PrayerRecord
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";
        public string PrayerName { get; set; } = "";
        public DateTime PrayerDate { get; set; }
        public TimeSpan PrayerTime { get; set; }
        public TimeSpan PrayerEndTime { get; set; }
        public DateTime? ActualCompletionTime { get; set; }
        public string Status { get; set; } = PrayerStatus.Upcoming;
        public string? CongregationType { get; set; }
        public Guid? UpdatedByUserId { get; set; }
        public string? UpdatedByUsername { get; set; }
        public string? Notes { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PrayerStatusHistory>? History { get; set; }
    }

    public class PrayerStatusHistory
    {
        public Guid Id { get; set; }
        public Guid PrayerRecordId { get; set; }
        public string OldStatus { get; set; } = "";
        public string NewStatus { get; set; } = "";
        public Guid? ChangedByUserId { get; set; }
        public string? ChangedByUsername { get; set; }
        public string? Note { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class PrayerReminderConfig
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid UserId { get; set; }
        public string PrayerName { get; set; } = "";
        public string ReminderType { get; set; } = "";
        public int MinutesOffset { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PrayerOrgSettings
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public double Latitude { get; set; } = 21.3891;
        public double Longitude { get; set; } = 39.8579;
        public string Timezone { get; set; } = "Asia/Riyadh";
        public string CalculationMethod { get; set; } = "MWL";
        public string AsrMethod { get; set; } = "Standard";
        public double FajrAngle { get; set; } = 18;
        public double IshaAngle { get; set; } = 17;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PrayerStreak
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid UserId { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public DateTime? LastStreakDate { get; set; }
        public int TotalPrayersCompleted { get; set; }
        public int TotalPrayersOnTime { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    public class PrayerDashboardResponse
    {
        public string TodayDate { get; set; } = "";
        public string TodayDay { get; set; } = "";
        public int CompletedCount { get; set; }
        public int PendingCount { get; set; }
        public int MissedCount { get; set; }
        public int ExcusedCount { get; set; }
        public int TotalPrayers { get; set; } = 5;
        public double CompletionPercentage { get; set; }
        public string? CurrentPrayer { get; set; }
        public string? NextPrayer { get; set; }
        public string? NextPrayerTime { get; set; }
        public int? MinutesToNextPrayer { get; set; }
        public PrayerStreak? Streak { get; set; }
        public List<PrayerRecord> Prayers { get; set; } = new();
        public PrayerOrgSettings? OrgSettings { get; set; }
    }

    public class PrayerHistoryDay
    {
        public string Date { get; set; } = "";
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; } = 5;
        public List<PrayerRecord> Prayers { get; set; } = new();
    }

    public class PrayerAdminSummary
    {
        public string Date { get; set; } = "";
        public int TotalUsers { get; set; }
        public int TotalPossiblePrayers { get; set; }
        public int TotalCompleted { get; set; }
        public int TotalMissed { get; set; }
        public int TotalPending { get; set; }
        public double OrgCompletionRate { get; set; }
        public List<PrayerUserStat> UserStats { get; set; } = new();
        public List<PrayerNameStat> PrayerStats { get; set; } = new();
    }

    public class PrayerUserStat
    {
        public string UserId { get; set; } = "";
        public string Username { get; set; } = "";
        public int CompletedToday { get; set; }
        public int CompletionRate { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public int TotalPrayersCompleted { get; set; }
    }

    public class PrayerNameStat
    {
        public string PrayerName { get; set; } = "";
        public int CompletedCount { get; set; }
        public int MissedCount { get; set; }
        public int PendingCount { get; set; }
        public double CompletionRate { get; set; }
    }

    public class UpdatePrayerStatusRequest
    {
        public string Status { get; set; } = "";
        public string? CongregationType { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateReminderConfigRequest
    {
        public string PrayerName { get; set; } = "";
        public string ReminderType { get; set; } = "";
        public int MinutesOffset { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class UpdateOrgSettingsRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Timezone { get; set; } = "";
        public string CalculationMethod { get; set; } = "MWL";
        public string AsrMethod { get; set; } = "Standard";
        public double FajrAngle { get; set; } = 18;
        public double IshaAngle { get; set; } = 17;
    }

    public class PrayerUserSettings
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid UserId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Timezone { get; set; }
        public string? CityName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class EffectivePrayerSettings
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Timezone { get; set; } = "";
        public string CalculationMethod { get; set; } = "MWL";
        public string AsrMethod { get; set; } = "Standard";
        public double FajrAngle { get; set; } = 18;
        public double IshaAngle { get; set; } = 17;
        public bool IsUserOverride { get; set; }
        public string? CityName { get; set; }
    }

    public class UpdateUserLocationRequest
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Timezone { get; set; }
        public string? CityName { get; set; }
    }
}
