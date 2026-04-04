using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class DivisionService
    {
        private readonly DivisionRepository _repo;

        public DivisionService(DivisionRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<Division>> GetAllAsync()
        {
            return await _repo.GetAllAsync();
        }

        public async Task<Division> CreateAsync(DivisionFormData request)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Division name is required");

            if (await _repo.NameExistsAsync(name))
                throw new ArgumentException("Division already exists");

            var division = new Division
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedAt = DateTime.UtcNow,
            };

            return await _repo.CreateAsync(division);
        }

        public async Task<bool> UpdateAsync(string id, DivisionFormData request)
        {
            if (!Guid.TryParse(id, out var guid))
                throw new ArgumentException("Invalid division ID");

            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Division name is required");

            if (await _repo.NameExistsForOtherAsync(name, guid))
                throw new ArgumentException("Division name already exists");

            return await _repo.UpdateAsync(guid, name);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (!Guid.TryParse(id, out var guid))
                throw new ArgumentException("Invalid division ID");

            var linkedCount = await _repo.GetLinkedRecordCountAsync(guid);
            if (linkedCount > 0)
                throw new ArgumentException($"Cannot delete division with {linkedCount} linked record{(linkedCount == 1 ? "" : "s")}. Reassign or remove them first.");

            return await _repo.DeleteAsync(guid);
        }
    }
}
