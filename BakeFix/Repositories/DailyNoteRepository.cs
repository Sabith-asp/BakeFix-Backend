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

        public async Task<DailyNote?> GetPersonalByDateAsync(DateTime date)
        {
            using var connection = new MySqlConnection(_conn);
            return await connection.QueryFirstOrDefaultAsync<DailyNote>(
                @"SELECT Id, OrganizationId, CreatedByUserId, CreatedByUsername,
                         NoteDate, Content, Visibility, CreatedAt, UpdatedAt
                  FROM DailyNotes
                  WHERE OrganizationId = @orgId AND CreatedByUserId = @userId
                    AND NoteDate = @date AND Visibility = 'Personal'",
                new { orgId = _tenant.RequiredOrgId, userId = _tenant.RequiredUserId, date });
        }

        public async Task<IEnumerable<DailyNote>> GetOrgNotesByDateAsync(DateTime date)
        {
            using var connection = new MySqlConnection(_conn);
            return await connection.QueryAsync<DailyNote>(
                @"SELECT Id, OrganizationId, CreatedByUserId, CreatedByUsername,
                         NoteDate, Content, Visibility, CreatedAt, UpdatedAt
                  FROM DailyNotes
                  WHERE OrganizationId = @orgId AND NoteDate = @date AND Visibility = 'Organisation'
                  ORDER BY CreatedAt ASC",
                new { orgId = _tenant.RequiredOrgId, date });
        }

        public async Task<IEnumerable<DailyNote>> GetRecentPersonalAsync(int limit)
        {
            using var connection = new MySqlConnection(_conn);
            return await connection.QueryAsync<DailyNote>(
                @"SELECT Id, CreatedByUserId, CreatedByUsername, NoteDate, Content, Visibility, CreatedAt, UpdatedAt
                  FROM DailyNotes
                  WHERE OrganizationId = @orgId AND CreatedByUserId = @userId AND Visibility = 'Personal'
                  ORDER BY NoteDate DESC
                  LIMIT @limit",
                new { orgId = _tenant.RequiredOrgId, userId = _tenant.RequiredUserId, limit });
        }

        public async Task<DailyNote> UpsertPersonalAsync(DailyNote note)
        {
            using var connection = new MySqlConnection(_conn);
            note.OrganizationId    = _tenant.RequiredOrgId;
            note.CreatedByUserId   = _tenant.RequiredUserId;
            note.CreatedByUsername = _tenant.Username;

            await connection.ExecuteAsync(
                @"INSERT INTO DailyNotes
                    (Id, OrganizationId, CreatedByUserId, CreatedByUsername,
                     NoteDate, Content, Visibility, CreatedAt, UpdatedAt)
                  VALUES
                    (@Id, @OrganizationId, @CreatedByUserId, @CreatedByUsername,
                     @NoteDate, @Content, @Visibility, @CreatedAt, @UpdatedAt)
                  ON DUPLICATE KEY UPDATE
                    Content   = VALUES(Content),
                    UpdatedAt = VALUES(UpdatedAt)",
                note);
            return note;
        }

        public async Task<DailyNote> UpsertOrgAsync(DailyNote note)
        {
            using var connection = new MySqlConnection(_conn);
            note.OrganizationId    = _tenant.RequiredOrgId;
            note.CreatedByUserId   = _tenant.RequiredUserId;
            note.CreatedByUsername = _tenant.Username;
            note.Visibility        = "Organisation";

            await connection.ExecuteAsync(
                @"INSERT INTO DailyNotes
                    (Id, OrganizationId, CreatedByUserId, CreatedByUsername,
                     NoteDate, Content, Visibility, CreatedAt, UpdatedAt)
                  VALUES
                    (@Id, @OrganizationId, @CreatedByUserId, @CreatedByUsername,
                     @NoteDate, @Content, @Visibility, @CreatedAt, @UpdatedAt)
                  ON DUPLICATE KEY UPDATE
                    Content   = VALUES(Content),
                    UpdatedAt = VALUES(UpdatedAt)",
                note);
            return note;
        }

        public async Task<bool> DeletePersonalByDateAsync(DateTime date)
        {
            using var connection = new MySqlConnection(_conn);
            int rows = await connection.ExecuteAsync(
                @"DELETE FROM DailyNotes
                  WHERE OrganizationId = @orgId AND CreatedByUserId = @userId
                    AND NoteDate = @date AND Visibility = 'Personal'",
                new { orgId = _tenant.RequiredOrgId, userId = _tenant.RequiredUserId, date });
            return rows > 0;
        }
    }
}
