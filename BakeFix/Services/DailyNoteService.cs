using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class DailyNoteService
    {
        private readonly DailyNoteRepository _repo;

        public DailyNoteService(DailyNoteRepository repo)
        {
            _repo = repo;
        }

        public Task<DailyNote?> GetPersonalByDateAsync(DateTime date)
            => _repo.GetPersonalByDateAsync(date);

        public Task<IEnumerable<DailyNote>> GetOrgNotesByDateAsync(DateTime date)
            => _repo.GetOrgNotesByDateAsync(date);

        public Task<IEnumerable<DailyNote>> GetRecentPersonalAsync(int limit = 7)
            => _repo.GetRecentPersonalAsync(limit);

        public async Task<DailyNote> UpsertAsync(string dateStr, UpsertNoteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                throw new ArgumentException("Note content cannot be empty.");
            if (!DateTime.TryParse(dateStr, out var date))
                throw new ArgumentException("Invalid date.");

            var note = new DailyNote
            {
                Id        = Guid.NewGuid(),
                NoteDate  = date,
                Content   = request.Content.Trim(),
                Visibility = request.Visibility == "Organisation" ? "Organisation" : "Personal",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return request.Visibility == "Organisation"
                ? await _repo.UpsertOrgAsync(note)
                : await _repo.UpsertPersonalAsync(note);
        }

        public Task<bool> DeletePersonalByDateAsync(DateTime date)
            => _repo.DeletePersonalByDateAsync(date);
    }
}
