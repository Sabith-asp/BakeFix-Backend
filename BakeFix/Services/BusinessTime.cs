namespace BakeFix.Services
{
    // "Today" for task scheduling must follow the business's local day, not the server's
    // UTC day — using DateTime.UtcNow.Date directly shifts the day boundary by the UTC
    // offset (5:30 for IST), which is why overdue/carry-forward was firing hours late.
    public static class BusinessTime
    {
        private static readonly TimeZoneInfo Zone = ResolveZone();

        private static TimeZoneInfo ResolveZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        }

        public static DateTime Today =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone).Date;

        public static DateTime NextMidnightUtc()
        {
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
            var nextLocalMidnight = localNow.Date.AddDays(1);
            return TimeZoneInfo.ConvertTimeToUtc(nextLocalMidnight, Zone);
        }
    }
}
