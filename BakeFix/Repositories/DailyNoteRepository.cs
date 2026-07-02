using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class DailyNoteRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public DailyNoteRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn   = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        private const string Cols = @"Id, OrganizationId, CreatedByUserId, CreatedByUsername,
                                      NoteDate, Title, Content, Visibility, CreatedAt, UpdatedAt";

        public async Task<IEnumerable<DailyNote>> GetPersonalByDateAsync(DateTime date)
        {
            using var connection = new MySqlConnection(_conn);
            return await connection.QueryAsync<DailyNote>(
                $@"SELECT {Cols} FROM DailyNotes
                   WHERE OrganizationId = @orgId AND CreatedByUserId = @userId
                     AND NoteDate = @date AND Visibility = 'Personal'
                   ORDER BY CreatedAt ASC",
                new { orgId = _tenant.RequiredOrgId, userId = _tenant.RequiredUserId, date });
        }

        public async Task<IEnumerable<DailyNote>> GetOrgNotesByDateAsync(DateTime date)
        {
            using var connection = new MySqlConnection(_conn);
            return await connection.QueryAsync<DailyNote>(
                $@"SELECT {Cols} FROM DailyNotes
                   WHERE OrganizationId = @orgId AND NoteDate = @date AND Visibility = 'Organisation'
                   ORDER BY CreatedAt ASC",
                new { orgId = _tenant.RequiredOrgId, date });
        }

        public async Task<DailyNote> CreateAsync(DailyNote note)
        {
            using var connection = new MySqlConnection(_conn);
            note.Id                = Guid.NewGuid();
            note.OrganizationId    = _tenant.RequiredOrgId;
            note.CreatedByUserId   = _tenant.RequiredUserId;
            note.CreatedByUsername = _tenant.Username;
            note.CreatedAt         = DateTime.UtcNow;
            note.UpdatedAt         = DateTime.UtcNow;

            await connection.ExecuteAsync(
                @"INSERT INTO DailyNotes
                    (Id, OrganizationId, CreatedByUserId, CreatedByUsername,
                     NoteDate, Title, Content, Visibility, CreatedAt, UpdatedAt)
                  VALUES
                    (@Id, @OrganizationId, @CreatedByUserId, @CreatedByUsername,
                     @NoteDate, @Title, @Content, @Visibility, @CreatedAt, @UpdatedAt)",
                note);
            return note;
        }

        public async Task<DailyNote?> UpdateAsync(Guid id, string content, string? title)
        {
            using var connection = new MySqlConnection(_conn);
            await connection.ExecuteAsync(
                @"UPDATE DailyNotes SET Content = @content, Title = @title, UpdatedAt = @now
                  WHERE Id = @id AND CreatedByUserId = @userId AND OrganizationId = @orgId",
                new { id, content, title, now = DateTime.UtcNow,
                      userId = _tenant.RequiredUserId, orgId = _tenant.RequiredOrgId });

            return await connection.QueryFirstOrDefaultAsync<DailyNote>(
                $"SELECT {Cols} FROM DailyNotes WHERE Id = @id", new { id });
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            using var connection = new MySqlConnection(_conn);
            int rows = await connection.ExecuteAsync(
                @"DELETE FROM DailyNotes WHERE Id = @id
                  AND CreatedByUserId = @userId AND OrganizationId = @orgId",
                new { id, userId = _tenant.RequiredUserId, orgId = _tenant.RequiredOrgId });
            return rows > 0;
        }
    }
}
