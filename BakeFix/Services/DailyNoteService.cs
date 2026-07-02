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

        public async Task<(IEnumerable<DailyNote> Personal, IEnumerable<DailyNote> OrgNotes)>
            GetByDateAsync(DateTime date)
        {
            var personal = await _repo.GetPersonalByDateAsync(date);
            var orgNotes = await _repo.GetOrgNotesByDateAsync(date);
            return (personal, orgNotes);
        }

        public async Task<DailyNote> CreateAsync(DateTime noteDate, string content, string? title, string visibility)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be empty.");

            var note = new DailyNote
            {
                NoteDate   = noteDate,
                Content    = content.Trim(),
                Title      = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
                Visibility = visibility is "Personal" or "Organisation" ? visibility : "Personal",
            };
            return await _repo.CreateAsync(note);
        }

        public async Task<DailyNote?> UpdateAsync(Guid id, string content, string? title)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be empty.");

            return await _repo.UpdateAsync(id, content.Trim(), string.IsNullOrWhiteSpace(title) ? null : title.Trim());
        }

        public async Task<bool> DeleteAsync(Guid id) => await _repo.DeleteAsync(id);
    }
}
