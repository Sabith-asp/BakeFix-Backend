using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class TaskRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public TaskRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn   = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        // My Tasks: created by me OR assigned to me, not deleted
        public async Task<IEnumerable<TaskItem>> GetMyTasksAsync(
            string view, string? status, string? priority, string? category, string? search)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;
            var today = _tenant.OrgLocalDate;

            var conditions = new List<string>
            {
                "t.OrganizationId = @orgId",
                "t.DeletedAt IS NULL",
                "(t.CreatedByUserId = @userId OR t.AssignedToUserId = @userId)"
            };

            switch (view)
            {
                case "today":
                    conditions.Add("t.CurrentTargetDate = @today");
                    conditions.Add("t.Status != 'Completed'");
                    break;
                case "pending":
                    conditions.Add("t.Status IN ('Pending','InProgress')");
                    break;
                case "overdue":
                    conditions.Add("t.CurrentTargetDate < @today");
                    conditions.Add("t.Status != 'Completed'");
                    break;
                case "completed":
                    conditions.Add("t.Status = 'Completed'");
                    break;
            }

            if (!string.IsNullOrWhiteSpace(status))   conditions.Add("t.Status = @status");
            if (!string.IsNullOrWhiteSpace(priority))  conditions.Add("t.Priority = @priority");
            if (!string.IsNullOrWhiteSpace(category))  conditions.Add("t.Category = @category");
            if (!string.IsNullOrWhiteSpace(search))
                conditions.Add("(t.Title LIKE @search OR t.Description LIKE @search)");

            var where = "WHERE " + string.Join(" AND ", conditions);
            var searchParam = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";

            var order = view == "completed"
                ? "ORDER BY t.CompletedAt DESC"
                : "ORDER BY FIELD(t.Priority,'High','Medium','Low'), t.CurrentTargetDate ASC, t.CreatedAt ASC";

            return await connection.QueryAsync<TaskItem>(
                $@"SELECT t.Id, t.OrganizationId, t.CreatedByUserId, t.CreatedByUsername,
                          t.AssignedToUserId, t.AssignedToUsername,
                          t.Title, t.Description, t.Category, t.Priority, t.Status, t.Visibility,
                          t.OriginalTargetDate, t.CurrentTargetDate, t.CompletedAt, t.CreatedAt, t.UpdatedAt
                   FROM Tasks t
                   {where}
                   {order}",
                new { orgId, userId, today, status, priority, category, search = searchParam });
        }

        // Team Tasks: org-level tasks visible to all org members, not deleted
        public async Task<IEnumerable<TaskItem>> GetTeamTasksAsync(
            string view, string? status, string? priority, string? category, string? search)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;
            var today = _tenant.OrgLocalDate;

            var conditions = new List<string>
            {
                "t.OrganizationId = @orgId",
                "t.Visibility = 'Organisation'",
                "t.DeletedAt IS NULL"
            };

            switch (view)
            {
                case "today":
                    conditions.Add("t.CurrentTargetDate = @today");
                    conditions.Add("t.Status != 'Completed'");
                    break;
                case "pending":
                    conditions.Add("t.Status IN ('Pending','InProgress')");
                    break;
                case "overdue":
                    conditions.Add("t.CurrentTargetDate < @today");
                    conditions.Add("t.Status != 'Completed'");
                    break;
                case "completed":
                    conditions.Add("t.Status = 'Completed'");
                    break;
            }

            if (!string.IsNullOrWhiteSpace(status))  conditions.Add("t.Status = @status");
            if (!string.IsNullOrWhiteSpace(priority)) conditions.Add("t.Priority = @priority");
            if (!string.IsNullOrWhiteSpace(category)) conditions.Add("t.Category = @category");
            if (!string.IsNullOrWhiteSpace(search))
                conditions.Add("(t.Title LIKE @search OR t.Description LIKE @search)");

            var where = "WHERE " + string.Join(" AND ", conditions);
            var searchParam = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";

            var order = view == "completed"
                ? "ORDER BY t.CompletedAt DESC"
                : "ORDER BY FIELD(t.Priority,'High','Medium','Low'), t.CurrentTargetDate ASC";

            return await connection.QueryAsync<TaskItem>(
                $@"SELECT t.Id, t.OrganizationId, t.CreatedByUserId, t.CreatedByUsername,
                          t.AssignedToUserId, t.AssignedToUsername,
                          t.Title, t.Description, t.Category, t.Priority, t.Status, t.Visibility,
                          t.OriginalTargetDate, t.CurrentTargetDate, t.CompletedAt, t.CreatedAt, t.UpdatedAt
                   FROM Tasks t
                   {where}
                   {order}",
                new { orgId, today, status, priority, category, search = searchParam });
        }

        public async Task<TaskItem?> GetByIdAsync(Guid id)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            var task = await connection.QueryFirstOrDefaultAsync<TaskItem>(
                @"SELECT t.Id, t.OrganizationId, t.CreatedByUserId, t.CreatedByUsername,
                         t.AssignedToUserId, t.AssignedToUsername,
                         t.Title, t.Description, t.Category, t.Priority, t.Status, t.Visibility,
                         t.OriginalTargetDate, t.CurrentTargetDate, t.CompletedAt, t.CreatedAt, t.UpdatedAt
                  FROM Tasks t
                  WHERE t.Id = @id AND t.OrganizationId = @orgId AND t.DeletedAt IS NULL
                    AND (t.Visibility = 'Organisation' OR t.CreatedByUserId = @userId OR t.AssignedToUserId = @userId)",
                new { id, orgId, userId });

            if (task is null) return null;

            task.ActivityLog = (await connection.QueryAsync<TaskActivity>(
                @"SELECT Id, TaskId, PerformedByUserId, PerformedByUsername,
                         ActivityType, OldValue, NewValue, Comment, CreatedAt
                  FROM TaskActivityLog
                  WHERE TaskId = @id
                  ORDER BY CreatedAt ASC",
                new { id })).ToList();

            return task;
        }

        public async Task<TaskItem> CreateAsync(TaskItem task)
        {
            using var connection = new MySqlConnection(_conn);
            await connection.ExecuteAsync(
                @"INSERT INTO Tasks
                    (Id, OrganizationId, CreatedByUserId, CreatedByUsername,
                     AssignedToUserId, AssignedToUsername,
                     Title, Description, Category, Priority, Status, Visibility,
                     OriginalTargetDate, CurrentTargetDate, CreatedAt, UpdatedAt)
                  VALUES
                    (@Id, @OrganizationId, @CreatedByUserId, @CreatedByUsername,
                     @AssignedToUserId, @AssignedToUsername,
                     @Title, @Description, @Category, @Priority, @Status, @Visibility,
                     @OriginalTargetDate, @CurrentTargetDate, @CreatedAt, @UpdatedAt)",
                task);
            return task;
        }

        public async Task<bool> UpdateAsync(TaskItem task)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            int rows = await connection.ExecuteAsync(
                @"UPDATE Tasks
                  SET Title = @Title, Description = @Description, Category = @Category,
                      Priority = @Priority, AssignedToUserId = @AssignedToUserId,
                      AssignedToUsername = @AssignedToUsername, UpdatedAt = @UpdatedAt
                  WHERE Id = @Id AND OrganizationId = @OrgId AND CreatedByUserId = @UserId AND DeletedAt IS NULL",
                new
                {
                    task.Title, task.Description, task.Category, task.Priority,
                    task.AssignedToUserId, task.AssignedToUsername, task.UpdatedAt,
                    task.Id, OrgId = orgId, UserId = userId
                });
            return rows > 0;
        }

        public async Task<bool> ChangeStatusAsync(Guid id, string status, DateTime? completedAt)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            // Org tasks can be completed by any org member; personal tasks only by creator
            int rows = await connection.ExecuteAsync(
                @"UPDATE Tasks
                  SET Status = @status, CompletedAt = @completedAt, UpdatedAt = @now
                  WHERE Id = @id AND OrganizationId = @orgId AND DeletedAt IS NULL
                    AND (Visibility = 'Organisation' OR CreatedByUserId = @userId OR AssignedToUserId = @userId)",
                new { status, completedAt, now = DateTime.UtcNow, id, orgId, userId });
            return rows > 0;
        }

        public async Task<bool> ChangeDateAsync(Guid id, DateTime newDate)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            int rows = await connection.ExecuteAsync(
                @"UPDATE Tasks
                  SET CurrentTargetDate = @newDate, UpdatedAt = @now
                  WHERE Id = @id AND OrganizationId = @orgId AND CreatedByUserId = @userId AND DeletedAt IS NULL",
                new { newDate, now = DateTime.UtcNow, id, orgId, userId });
            return rows > 0;
        }

        public async Task<bool> ChangeVisibilityAsync(Guid id, string visibility)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            int rows = await connection.ExecuteAsync(
                @"UPDATE Tasks
                  SET Visibility = @visibility, UpdatedAt = @now
                  WHERE Id = @id AND OrganizationId = @orgId AND CreatedByUserId = @userId AND DeletedAt IS NULL",
                new { visibility, now = DateTime.UtcNow, id, orgId, userId });
            return rows > 0;
        }


        public async Task<bool> ChangePriorityAsync(Guid id, string priority)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            // Org tasks can be re-prioritised by any org member; personal tasks only by creator
            int rows = await connection.ExecuteAsync(
                @"UPDATE Tasks
                  SET Priority = @priority, UpdatedAt = @now
                  WHERE Id = @id AND OrganizationId = @orgId AND DeletedAt IS NULL
                    AND (Visibility = 'Organisation' OR CreatedByUserId = @userId OR AssignedToUserId = @userId)",
                new { priority, now = DateTime.UtcNow, id, orgId, userId });
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            int rows = await connection.ExecuteAsync(
                "DELETE FROM Tasks WHERE Id = @id AND OrganizationId = @orgId AND CreatedByUserId = @userId",
                new { id, orgId, userId });
            return rows > 0;
        }

        public async Task LogActivityAsync(TaskActivity log)
        {
            using var connection = new MySqlConnection(_conn);
            await connection.ExecuteAsync(
                @"INSERT INTO TaskActivityLog
                    (Id, TaskId, PerformedByUserId, PerformedByUsername,
                     ActivityType, OldValue, NewValue, Comment, CreatedAt)
                  VALUES
                    (@Id, @TaskId, @PerformedByUserId, @PerformedByUsername,
                     @ActivityType, @OldValue, @NewValue, @Comment, @CreatedAt)",
                log);
        }

        // Used by carry-forward background service — no tenant scope
        public async Task<IEnumerable<TaskItem>> GetOverdueAllOrgsAsync(string conn)
        {
            using var connection = new MySqlConnection(conn);
            var today = _tenant.OrgLocalDate;
            return await connection.QueryAsync<TaskItem>(
                @"SELECT Id, OrganizationId, CreatedByUserId, CreatedByUsername,
                         CurrentTargetDate
                  FROM Tasks
                  WHERE CurrentTargetDate < @today
                    AND Status IN ('Pending','InProgress')
                    AND DeletedAt IS NULL",
                new { today });
        }

        public async Task BulkCarryForwardAsync(string conn, IEnumerable<Guid> ids, DateTime newDate)
        {
            using var connection = new MySqlConnection(conn);
            await connection.ExecuteAsync(
                "UPDATE Tasks SET CurrentTargetDate = @newDate, UpdatedAt = @now WHERE Id IN @ids",
                new { newDate, now = DateTime.UtcNow, ids });
        }

        public async Task BulkLogActivityAsync(string conn, IEnumerable<TaskActivity> logs)
        {
            using var connection = new MySqlConnection(conn);
            foreach (var log in logs)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO TaskActivityLog
                        (Id, TaskId, PerformedByUserId, PerformedByUsername,
                         ActivityType, OldValue, NewValue, Comment, CreatedAt)
                      VALUES
                        (@Id, @TaskId, @PerformedByUserId, @PerformedByUsername,
                         @ActivityType, @OldValue, @NewValue, @Comment, @CreatedAt)",
                    log);
            }
        }

        public async Task<(int TodayCount, int OverdueCount)> GetSummaryCountsAsync()
        {
            using var connection = new MySqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;
            var today = _tenant.OrgLocalDate;

            var todayCount = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*) FROM Tasks
                  WHERE OrganizationId = @orgId AND DeletedAt IS NULL
                    AND (CreatedByUserId = @userId OR AssignedToUserId = @userId)
                    AND CurrentTargetDate = @today AND Status != 'Completed'",
                new { orgId, userId, today });

            var overdueCount = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*) FROM Tasks
                  WHERE OrganizationId = @orgId AND DeletedAt IS NULL
                    AND (CreatedByUserId = @userId OR AssignedToUserId = @userId)
                    AND CurrentTargetDate < @today AND Status != 'Completed'",
                new { orgId, userId, today });

            return (todayCount, overdueCount);
        }
    }
}
