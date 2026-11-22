namespace BakeFix.Services
{
    // Pure TimeZoneInfo resolve + UTC<->local conversion, shared by the per-request
    // ITenantContext (org business timezone) and background jobs that have no
    // per-request context (TaskCarryForwardService, NotificationSchedulerService,
    // PrayerSchedulerService/PrayerService — the latter two use PrayerOrgSettings/
    // PrayerUserSettings.Timezone, a deliberately separate data source from
    // Organizations.Timezone; this class only shares the conversion mechanics,
    // never the data source).
    public static class OrgTimeConversion
    {
        public static TimeZoneInfo ResolveZone(string ianaId)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(ianaId); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
            catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
        }

        public static DateTime ToLocal(DateTime utcNow, string ianaId) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), ResolveZone(ianaId));

        public static DateTime LocalDate(DateTime utcNow, string ianaId) =>
            ToLocal(utcNow, ianaId).Date;
    }
}
