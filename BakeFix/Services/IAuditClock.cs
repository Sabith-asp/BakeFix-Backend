namespace BakeFix.Services
{
    // Single source of truth for "what time is it, in UTC, right now" for audit
    // timestamps (CreatedAt/UpdatedAt/etc.). Backed by TimeProvider so tests can
    // inject a fake clock instead of depending on wall-clock time.
    public interface IAuditClock
    {
        DateTime UtcNow { get; }
    }

    public class AuditClock : IAuditClock
    {
        private readonly TimeProvider _timeProvider;

        public AuditClock(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;
    }
}
