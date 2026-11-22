using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using Npgsql;

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

        // Shared SELECT list and "manual order wins, otherwise most-recently-touched first" sort —
        // a drag-to-reorder always stamps every card in the column with a SortOrder row (see
        // ReorderAsync), so anything still NULL here has never been manually arranged and floats
        // by recency instead; the moment a user drags anything in a column, that visual order
        // (recency-based until then) gets captured as the new manual order going forward.
        private const string TaskSelectColumns =
            @"t.Id, t.OrganizationId, t.CreatedByUserId, t.CreatedByUsername,
              t.AssignedToUserId, t.AssignedToUsername,
              t.Title, t.Description, t.Category, t.Priority, t.Status, t.Visibility,
              t.OriginalTargetDate, t.CurrentTargetDate, t.CompletedAt,
              t.NeedsFollowUp, t.FollowUpDate, t.FollowUpNotes, t.FollowUpResolvedAt,
              t.CreatedAt, t.UpdatedAt, so.SortOrder";

        private const string ActiveOrder =
            "ORDER BY (so.SortOrder IS NULL), so.SortOrder ASC, t.UpdatedAt DESC, t.CreatedAt DESC";

        // My Tasks: created by me OR assigned to me, not deleted.
        // "completed" and the completed slice of "all" (the Kanban board) are cursor-paginated —
        // only Completed grows without bound, so only it needs "Load more" instead of one giant
        // fetch; `before` is the previous page's oldest CompletedAt, null for the first page.
        public async Task<(IEnumerable<TaskItem> Items, string? NextCursor)> GetMyTasksAsync(
            string view, string? status, string? priority, string? category, string? search,
            DateTime? before, int limit, bool? needsFollowUp = null)
        {
            using var connection = new NpgsqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;
            var today  = _tenant.OrgLocalDate;

            var baseConditions = new List<string>
            {
                "t.OrganizationId = @orgId",
                "t.DeletedAt IS NULL",
                "(t.CreatedByUserId = @userId OR t.AssignedToUserId = @userId)"
            };
            if (!string.IsNullOrWhiteSpace(status))   baseConditions.Add("t.Status = @status");
            if (!string.IsNullOrWhiteSpace(priority))  baseConditions.Add("t.Priority = @priority");
            if (!string.IsNullOrWhiteSpace(category))  baseConditions.Add("t.Category = @category");
            if (!string.IsNullOrWhiteSpace(search))
                baseConditions.Add("(t.Title ILIKE @search OR t.Description ILIKE @search)");
            // Only a Completed task ever carries NeedsFollowUp, but this composes with any view
            // (unlike the "followups" view below, which replaces the whole filter set) so it can
            // narrow the Kanban board's "all" view down to just the follow-ups within it.
            if (needsFollowUp == true)
            {
                baseConditions.Add("t.NeedsFollowUp = TRUE");
                baseConditions.Add("t.FollowUpResolvedAt IS NULL");
            }

            var searchParam = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
            var sortJoin = "LEFT JOIN TaskSortOrder so ON so.TaskId = t.Id AND so.ScopeType = 'User' AND so.ScopeId = @userId";
            var queryParams = new { orgId, userId, today, status, priority, category, search = searchParam, before, fetchLimit = limit + 1 };

            if (view == "all")
            {
                // Pending/InProgress stay small by nature — fetch them in full, unpaginated.
                // Completed is the one bucket that only ever grows, so it alone is paginated;
                // a "load more" request (before != null) only ever asks for more of it.
                IEnumerable<TaskItem> activeItems = Array.Empty<TaskItem>();
                if (!before.HasValue)
                {
                    var activeWhere = "WHERE " + string.Join(" AND ", baseConditions.Append("t.Status != 'Completed'"));
                    activeItems = await connection.QueryAsync<TaskItem>(
                        $"SELECT {TaskSelectColumns} FROM Tasks t {sortJoin} {activeWhere} {ActiveOrder}", queryParams);
                }

                var completedConditions = new List<string>(baseConditions) { "t.Status = 'Completed'" };
                if (before.HasValue) completedConditions.Add("t.CompletedAt < @before");
                var completedWhere = "WHERE " + string.Join(" AND ", completedConditions);
                var completedRows = (await connection.QueryAsync<TaskItem>(
                    $"SELECT {TaskSelectColumns} FROM Tasks t {sortJoin} {completedWhere} ORDER BY t.CompletedAt DESC LIMIT @fetchLimit",
                    queryParams)).ToList();

                string? boardCursor = null;
                if (completedRows.Count > limit)
                {
                    completedRows.RemoveAt(completedRows.Count - 1);
                    boardCursor = completedRows[^1].CompletedAt!.Value.ToString("O");
                }
                return (activeItems.Concat(completedRows), boardCursor);
            }

            if (view == "completed")
            {
                var conditions = new List<string>(baseConditions) { "t.Status = 'Completed'" };
                if (before.HasValue) conditions.Add("t.CompletedAt < @before");
                var where = "WHERE " + string.Join(" AND ", conditions);
                var rows = (await connection.QueryAsync<TaskItem>(
                    $"SELECT {TaskSelectColumns} FROM Tasks t {sortJoin} {where} ORDER BY t.CompletedAt DESC LIMIT @fetchLimit",
                    queryParams)).ToList();

                string? cursor = null;
                if (rows.Count > limit)
                {
                    rows.RemoveAt(rows.Count - 1);
                    cursor = rows[^1].CompletedAt!.Value.ToString("O");
                }
                return (rows, cursor);
            }

            var fallbackConditions = new List<string>(baseConditions);
            switch (view)
            {
                case "today":
                    fallbackConditions.Add("t.CurrentTargetDate = @today");
                    fallbackConditions.Add("t.Status != 'Completed'");
                    break;
                case "pending":
                    fallbackConditions.Add("t.Status IN ('Pending','InProgress')");
                    break;
                case "overdue":
                    fallbackConditions.Add("t.CurrentTargetDate < @today");
                    fallbackConditions.Add("t.Status != 'Completed'");
                    break;
                case "followups":
                    fallbackConditions.Add("t.NeedsFollowUp = TRUE");
                    fallbackConditions.Add("t.FollowUpResolvedAt IS NULL");
                    fallbackConditions.Add("t.Status = 'Completed'");
                    break;
            }
            var fallbackWhere = "WHERE " + string.Join(" AND ", fallbackConditions);
            var fallbackOrder = view == "followups" ? "ORDER BY t.FollowUpDate ASC, t.CreatedAt ASC" : ActiveOrder;

            var fallbackRows = await connection.QueryAsync<TaskItem>(
                $"SELECT {TaskSelectColumns} FROM Tasks t {sortJoin} {fallbackWhere} {fallbackOrder}", queryParams);
            return (fallbackRows, null);
        }

        // Team Tasks: org-level tasks visible to all org members, not deleted
        public async Task<(IEnumerable<TaskItem> Items, string? NextCursor)> GetTeamTasksAsync(
            string view, string? status, string? priority, string? category, string? search,
            DateTime? before, int limit, bool? needsFollowUp = null)
        {
            using var connection = new NpgsqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;
            var today = _tenant.OrgLocalDate;

            var baseConditions = new List<string>
            {
                "t.OrganizationId = @orgId",
                "t.Visibility = 'Organisation'",
                "t.DeletedAt IS NULL"
            };
            if (!string.IsNullOrWhiteSpace(status))  baseConditions.Add("t.Status = @status");
            if (!string.IsNullOrWhiteSpace(priority)) baseConditions.Add("t.Priority = @priority");
            if (!string.IsNullOrWhiteSpace(category)) baseConditions.Add("t.Category = @category");
            if (!string.IsNullOrWhiteSpace(search))
                baseConditions.Add("(t.Title ILIKE @search OR t.Description ILIKE @search)");
            if (needsFollowUp == true)
            {
                baseConditions.Add("t.NeedsFollowUp = TRUE");
                baseConditions.Add("t.FollowUpResolvedAt IS NULL");
            }

            var searchParam = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
            var sortJoin = "LEFT JOIN TaskSortOrder so ON so.TaskId = t.Id AND so.ScopeType = 'Team' AND so.ScopeId = @orgId";
            var queryParams = new { orgId, today, status, priority, category, search = searchParam, before, fetchLimit = limit + 1 };

            if (view == "all")
            {
                IEnumerable<TaskItem> activeItems = Array.Empty<TaskItem>();
                if (!before.HasValue)
                {
                    var activeWhere = "WHERE " + string.Join(" AND ", baseConditions.Append("t.Status != 'Completed'"));
                    activeItems = await connection.QueryAsync<TaskItem>(
                        $"SELECT {TaskSelectColumns} FROM Tasks t {sortJoin} {activeWhere} {ActiveOrder}", queryParams);
                }

                var completedConditions = new List<string>(baseConditions) { "t.Status = 'Completed'" };
                if (before.HasValue) completedConditions.Add("t.CompletedAt < @before");
                var completedWhere = "WHERE " + string.Join(" AND ", completedConditions);
                var completedRows = (await connection.QueryAsync<TaskItem>(
                    $"SELECT {TaskSelectColumns} FROM Tasks t {sortJoin} {completedWhere} ORDER BY t.CompletedAt DESC LIMIT @fetchLimit",
                    queryParams)).ToList();

                string? boardCursor = null;
                if (completedRows.Count > limit)
                {
                    completedRows.RemoveAt(completedRows.Count - 1);
                    boardCursor = completedRows[^1].CompletedAt!.Value.ToString("O");
                }
                return (activeItems.Concat(completedRows), boardCursor);
            }

            if (view == "completed")
            {
                var conditions = new List<string>(baseConditions) { "t.Status = 'Completed'" };
                if (before.HasValue) conditions.Add("t.CompletedAt < @before");
                var where = "WHERE " + string.Join(" AND ", conditions);
                var rows = (await connection.QueryAsync<TaskItem>(
                    $"SELECT {TaskSelectColumns} FROM Tasks t {sortJoin} {where} ORDER BY t.CompletedAt DESC LIMIT @fetchLimit",
                    queryParams)).ToList();

                string? cursor = null;
                if (rows.Count > limit)
                {
                    rows.RemoveAt(rows.Count - 1);
                    cursor = rows[^1].CompletedAt!.Value.ToString("O");
                }
                return (rows, cursor);
            }

            var fallbackConditions = new List<string>(baseConditions);
            switch (view)
            {
                case "today":
                    fallbackConditions.Add("t.CurrentTargetDate = @today");
                    fallbackConditions.Add("t.Status != 'Completed'");
                    break;
                case "pending":
                    fallbackConditions.Add("t.Status IN ('Pending','InProgress')");
                    break;
                case "overdue":
                    fallbackConditions.Add("t.CurrentTargetDate < @today");
                    fallbackConditions.Add("t.Status != 'Completed'");
                    break;
                case "followups":
                    fallbackConditions.Add("t.NeedsFollowUp = TRUE");
                    fallbackConditions.Add("t.FollowUpResolvedAt IS NULL");
                    fallbackConditions.Add("t.Status = 'Completed'");
                    break;
            }
            var fallbackWhere = "WHERE " + string.Join(" AND ", fallbackConditions);
            var fallbackOrder = view == "followups" ? "ORDER BY t.FollowUpDate ASC, t.CreatedAt ASC" : ActiveOrder;

            var fallbackRows = await connection.QueryAsync<TaskItem>(
                $"SELECT {TaskSelectColumns} FROM Tasks t {sortJoin} {fallbackWhere} {fallbackOrder}", queryParams);
            return (fallbackRows, null);
        }

        // Which of these task ids actually belong to this org (and, for the shared "Team" scope,
        // are org-visible) — guards reorder against ids from another tenant or a private task.
        public async Task<HashSet<Guid>> FilterValidTaskIdsAsync(IEnumerable<Guid> ids, bool requireOrgVisible)
        {
            using var connection = new NpgsqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            var sql = requireOrgVisible
                ? @"SELECT Id FROM Tasks
                    WHERE Id = ANY(@ids) AND OrganizationId = @orgId AND Visibility = 'Organisation' AND DeletedAt IS NULL"
                : @"SELECT Id FROM Tasks
                    WHERE Id = ANY(@ids) AND OrganizationId = @orgId AND DeletedAt IS NULL
                      AND (CreatedByUserId = @userId OR AssignedToUserId = @userId)";

            // Dapper's automatic "IN @list" expansion doesn't fire with this
            // Dapper/Npgsql combination (confirmed: fails identically for any
            // element type, with or without custom type handlers) — using
            // Postgres's native = ANY(array) instead, which also needs the
            // ids passed as text since our Id columns are CHAR(36), not uuid.
            var idStrings = ids.Select(i => i.ToString()).ToArray();
            var rows = await connection.QueryAsync<Guid>(sql, new { ids = idStrings, orgId, userId });
            return rows.ToHashSet();
        }

        // Upserts the manual order for a scope ("User"+userId for a private My Tasks order,
        // "Team"+orgId for the shared order everyone in the org sees).
        public async Task ReorderAsync(string scopeType, Guid scopeId, List<Guid> orderedTaskIds)
        {
            using var connection = new NpgsqlConnection(_conn);
            connection.Open();
            using var dbTx = connection.BeginTransaction();

            try
            {
                var now = DateTime.UtcNow;
                for (int i = 0; i < orderedTaskIds.Count; i++)
                {
                    await connection.ExecuteAsync(
                        @"INSERT INTO TaskSortOrder (Id, TaskId, ScopeType, ScopeId, SortOrder, UpdatedAt)
                          VALUES (@id, @taskId, @scopeType, @scopeId, @sortOrder, @now)
                          ON CONFLICT (ScopeType, ScopeId, TaskId) DO UPDATE SET SortOrder = @sortOrder, UpdatedAt = @now",
                        new
                        {
                            id = Guid.NewGuid(), taskId = orderedTaskIds[i], scopeType, scopeId,
                            sortOrder = i, now
                        },
                        dbTx);
                }
                dbTx.Commit();
            }
            catch
            {
                dbTx.Rollback();
                throw;
            }
        }

        public async Task<TaskItem?> GetByIdAsync(Guid id)
        {
            using var connection = new NpgsqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            var task = await connection.QueryFirstOrDefaultAsync<TaskItem>(
                @"SELECT t.Id, t.OrganizationId, t.CreatedByUserId, t.CreatedByUsername,
                         t.AssignedToUserId, t.AssignedToUsername,
                         t.Title, t.Description, t.Category, t.Priority, t.Status, t.Visibility,
                         t.OriginalTargetDate, t.CurrentTargetDate, t.CompletedAt,
                         t.NeedsFollowUp, t.FollowUpDate, t.FollowUpNotes, t.FollowUpResolvedAt,
                         t.CreatedAt, t.UpdatedAt
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
                  ORDER BY CreatedAt DESC",
                new { id })).ToList();

            return task;
        }

        public async Task<TaskItem> CreateAsync(TaskItem task)
        {
            using var connection = new NpgsqlConnection(_conn);
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
            using var connection = new NpgsqlConnection(_conn);
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
            using var connection = new NpgsqlConnection(_conn);
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
            using var connection = new NpgsqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            int rows = await connection.ExecuteAsync(
                @"UPDATE Tasks
                  SET CurrentTargetDate = @newDate, UpdatedAt = @now
                  WHERE Id = @id AND OrganizationId = @orgId AND CreatedByUserId = @userId AND DeletedAt IS NULL",
                new { newDate, now = DateTime.UtcNow, id, orgId, userId });
            return rows > 0;
        }

        public async Task<bool> SetFollowUpAsync(Guid id, bool needsFollowUp, DateTime? followUpDate, string? followUpNotes)
        {
            using var connection = new NpgsqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            // Same access rule as status/priority changes: any org member for an org-visible
            // task, otherwise only the creator or assignee.
            int rows = await connection.ExecuteAsync(
                @"UPDATE Tasks
                  SET NeedsFollowUp = @needsFollowUp, FollowUpDate = @followUpDate,
                      FollowUpNotes = @followUpNotes, FollowUpResolvedAt = NULL, UpdatedAt = @now
                  WHERE Id = @id AND OrganizationId = @orgId AND DeletedAt IS NULL
                    AND (Visibility = 'Organisation' OR CreatedByUserId = @userId OR AssignedToUserId = @userId)",
                new { needsFollowUp, followUpDate, followUpNotes, now = DateTime.UtcNow, id, orgId, userId });
            return rows > 0;
        }

        public async Task<bool> ResolveFollowUpAsync(Guid id)
        {
            using var connection = new NpgsqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            int rows = await connection.ExecuteAsync(
                @"UPDATE Tasks
                  SET FollowUpResolvedAt = @now, UpdatedAt = @now
                  WHERE Id = @id AND OrganizationId = @orgId AND DeletedAt IS NULL
                    AND NeedsFollowUp = TRUE AND FollowUpResolvedAt IS NULL
                    AND (Visibility = 'Organisation' OR CreatedByUserId = @userId OR AssignedToUserId = @userId)",
                new { now = DateTime.UtcNow, id, orgId, userId });
            return rows > 0;
        }

        public async Task<bool> ChangeVisibilityAsync(Guid id, string visibility)
        {
            using var connection = new NpgsqlConnection(_conn);
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
            using var connection = new NpgsqlConnection(_conn);
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
            using var connection = new NpgsqlConnection(_conn);
            var orgId  = _tenant.RequiredOrgId;
            var userId = _tenant.RequiredUserId;

            int rows = await connection.ExecuteAsync(
                "DELETE FROM Tasks WHERE Id = @id AND OrganizationId = @orgId AND CreatedByUserId = @userId",
                new { id, orgId, userId });
            return rows > 0;
        }

        public async Task LogActivityAsync(TaskActivity log)
        {
            using var connection = new NpgsqlConnection(_conn);
            await connection.ExecuteAsync(
                @"INSERT INTO TaskActivityLog
                    (Id, TaskId, PerformedByUserId, PerformedByUsername,
                     ActivityType, OldValue, NewValue, Comment, CreatedAt)
                  VALUES
                    (@Id, @TaskId, @PerformedByUserId, @PerformedByUsername,
                     @ActivityType, @OldValue, @NewValue, @Comment, @CreatedAt)",
                log);
        }

        // Used by carry-forward background service — no tenant scope. Each org supplies
        // its own already-resolved local "today" (via OrgTimeConversion), since this runs
        // outside any per-request HttpContext and must not rely on ITenantContext.
        public async Task<IEnumerable<TaskItem>> GetOverdueForOrgAsync(string conn, Guid orgId, DateTime orgLocalToday)
        {
            using var connection = new NpgsqlConnection(conn);
            return await connection.QueryAsync<TaskItem>(
                @"SELECT Id, OrganizationId, CreatedByUserId, CreatedByUsername,
                         CurrentTargetDate
                  FROM Tasks
                  WHERE OrganizationId = @orgId
                    AND CurrentTargetDate < @orgLocalToday
                    AND Status IN ('Pending','InProgress')
                    AND DeletedAt IS NULL",
                new { orgId, orgLocalToday });
        }

        public async Task BulkCarryForwardAsync(string conn, IEnumerable<Guid> ids, DateTime newDate)
        {
            using var connection = new NpgsqlConnection(conn);
            var idStrings = ids.Select(i => i.ToString()).ToArray();
            await connection.ExecuteAsync(
                "UPDATE Tasks SET CurrentTargetDate = @newDate, UpdatedAt = @now WHERE Id = ANY(@ids)",
                new { newDate, now = DateTime.UtcNow, ids = idStrings });
        }

        public async Task BulkLogActivityAsync(string conn, IEnumerable<TaskActivity> logs)
        {
            using var connection = new NpgsqlConnection(conn);
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
            using var connection = new NpgsqlConnection(_conn);
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
