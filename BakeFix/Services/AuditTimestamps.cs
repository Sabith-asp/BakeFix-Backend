namespace BakeFix.Services
{
    public static class AuditTimestamps
    {
        // Called immediately before INSERT. If a caller forgot to set CreatedAt
        // (left at default(DateTime) = 0001-01-01), stamp it here as a structural
        // backstop instead of relying on the DB's DEFAULT CURRENT_TIMESTAMP.
        public static DateTime ForCreate(DateTime current, IAuditClock clock) =>
            current == default ? clock.UtcNow : current;
    }
}
