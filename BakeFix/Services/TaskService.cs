using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class TaskService
    {
        private readonly TaskRepository _repo;
        private readonly ITenantContext _tenant;
        private readonly IAuditClock _clock;

        public TaskService(TaskRepository repo, ITenantContext tenant, IAuditClock clock)
        {
            _repo   = repo;
            _tenant = tenant;
            _clock  = clock;
        }

        public Task<(IEnumerable<TaskItem> Items, string? NextCursor)> GetMyTasksAsync(
            string view, string? status, string? priority, string? category, string? search,
            DateTime? before, int limit, bool? needsFollowUp = null)
            => _repo.GetMyTasksAsync(view, status, priority, category, search, before, Math.Clamp(limit, 1, 100), needsFollowUp);

        public Task<(IEnumerable<TaskItem> Items, string? NextCursor)> GetTeamTasksAsync(
            string view, string? status, string? priority, string? category, string? search,
            DateTime? before, int limit, bool? needsFollowUp = null)
            => _repo.GetTeamTasksAsync(view, status, priority, category, search, before, Math.Clamp(limit, 1, 100), needsFollowUp);

        public async Task<TaskItem?> GetByIdAsync(Guid id)
            => await _repo.GetByIdAsync(id);

        public async Task<TaskItem> CreateAsync(CreateTaskRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Title is required.");
            if (string.IsNullOrWhiteSpace(request.Category))
                throw new ArgumentException("Category is required.");
            if (!DateTime.TryParse(request.CurrentTargetDate, out var targetDate))
                throw new ArgumentException("Invalid target date.");

            var validPriorities = new[] { "High", "Medium", "Low" };
            if (!validPriorities.Contains(request.Priority))
                throw new ArgumentException("Invalid priority.");

            var validVisibility = new[] { "Personal", "Organisation" };
            if (!validVisibility.Contains(request.Visibility))
                throw new ArgumentException("Invalid visibility.");

            var task = new TaskItem
            {
                Id                  = Guid.NewGuid(),
                OrganizationId      = _tenant.RequiredOrgId,
                CreatedByUserId     = _tenant.RequiredUserId,
                CreatedByUsername   = _tenant.Username,
                AssignedToUserId    = request.Visibility == "Organisation" && !string.IsNullOrWhiteSpace(request.AssignedToUserId)
                                        ? Guid.Parse(request.AssignedToUserId) : null,
                AssignedToUsername  = request.Visibility == "Organisation" ? request.AssignedToUsername : null,
                Title               = request.Title.Trim(),
                Description         = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Category            = request.Category,
                Priority            = request.Priority,
                Status              = "Pending",
                Visibility          = request.Visibility,
                OriginalTargetDate  = targetDate,
                CurrentTargetDate   = targetDate,
                CreatedAt           = _clock.UtcNow,
                UpdatedAt           = _clock.UtcNow
            };

            await _repo.CreateAsync(task);
            await _repo.LogActivityAsync(new TaskActivity
            {
                Id                  = Guid.NewGuid(),
                TaskId              = task.Id,
                PerformedByUserId   = _tenant.RequiredUserId,
                PerformedByUsername = _tenant.Username,
                ActivityType        = "Created",
                CreatedAt           = _clock.UtcNow
            });

            return task;
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateTaskRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Title is required.");
            if (string.IsNullOrWhiteSpace(request.Category))
                throw new ArgumentException("Category is required.");

            var task = new TaskItem
            {
                Id                 = id,
                Title              = request.Title.Trim(),
                Description        = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Category           = request.Category,
                Priority           = request.Priority,
                AssignedToUserId   = !string.IsNullOrWhiteSpace(request.AssignedToUserId) ? Guid.Parse(request.AssignedToUserId) : null,
                AssignedToUsername = request.AssignedToUsername,
                UpdatedAt          = _clock.UtcNow
            };

            var updated = await _repo.UpdateAsync(task);
            if (updated)
            {
                await _repo.LogActivityAsync(new TaskActivity
                {
                    Id                  = Guid.NewGuid(),
                    TaskId              = id,
                    PerformedByUserId   = _tenant.RequiredUserId,
                    PerformedByUsername = _tenant.Username,
                    ActivityType        = "Updated",
                    CreatedAt           = _clock.UtcNow
                });
            }
            return updated;
        }

        public async Task<bool> ChangeStatusAsync(Guid id, string status)
        {
            var valid = new[] { "Pending", "InProgress", "Completed" };
            if (!valid.Contains(status))
                throw new ArgumentException("Invalid status value.");

            var existing = await _repo.GetByIdAsync(id)
                ?? throw new ArgumentException("Task not found.");

            DateTime? completedAt = status == "Completed" ? _clock.UtcNow : null;
            var updated = await _repo.ChangeStatusAsync(id, status, completedAt);

            if (updated)
            {
                await _repo.LogActivityAsync(new TaskActivity
                {
                    Id                  = Guid.NewGuid(),
                    TaskId              = id,
                    PerformedByUserId   = _tenant.RequiredUserId,
                    PerformedByUsername = _tenant.Username,
                    ActivityType        = status == "Completed" ? "Completed" : "StatusChanged",
                    OldValue            = existing.Status,
                    NewValue            = status,
                    CreatedAt           = _clock.UtcNow
                });
            }
            return updated;
        }

        public async Task<bool> ChangeDateAsync(Guid id, string newDateStr)
        {
            if (!DateTime.TryParse(newDateStr, out var newDate))
                throw new ArgumentException("Invalid date.");

            var existing = await _repo.GetByIdAsync(id)
                ?? throw new ArgumentException("Task not found.");

            var updated = await _repo.ChangeDateAsync(id, newDate);
            if (updated)
            {
                await _repo.LogActivityAsync(new TaskActivity
                {
                    Id                  = Guid.NewGuid(),
                    TaskId              = id,
                    PerformedByUserId   = _tenant.RequiredUserId,
                    PerformedByUsername = _tenant.Username,
                    ActivityType        = "DateMoved",
                    OldValue            = existing.CurrentTargetDate.ToString("yyyy-MM-dd"),
                    NewValue            = newDate.ToString("yyyy-MM-dd"),
                    CreatedAt           = _clock.UtcNow
                });
            }
            return updated;
        }

        // Follow-ups only make sense on a task that's already done; setting one requires a date.
        public async Task<bool> SetFollowUpAsync(Guid id, SetFollowUpRequest request)
        {
            var existing = await _repo.GetByIdAsync(id)
                ?? throw new ArgumentException("Task not found.");

            if (request.NeedsFollowUp)
            {
                if (existing.Status != "Completed")
                    throw new ArgumentException("Only a completed task can have a follow-up.");
                if (!DateTime.TryParse(request.FollowUpDate, out var parsedDate))
                    throw new ArgumentException("A follow-up date is required.");

                var updated = await _repo.SetFollowUpAsync(id, true, parsedDate, request.FollowUpNotes?.Trim());
                if (updated)
                {
                    await _repo.LogActivityAsync(new TaskActivity
                    {
                        Id                  = Guid.NewGuid(),
                        TaskId              = id,
                        PerformedByUserId   = _tenant.RequiredUserId,
                        PerformedByUsername = _tenant.Username,
                        ActivityType        = "FollowUpSet",
                        NewValue            = parsedDate.ToString("yyyy-MM-dd"),
                        CreatedAt           = _clock.UtcNow
                    });
                }
                return updated;
            }
            else
            {
                var updated = await _repo.SetFollowUpAsync(id, false, null, null);
                if (updated && existing.NeedsFollowUp)
                {
                    await _repo.LogActivityAsync(new TaskActivity
                    {
                        Id                  = Guid.NewGuid(),
                        TaskId              = id,
                        PerformedByUserId   = _tenant.RequiredUserId,
                        PerformedByUsername = _tenant.Username,
                        ActivityType        = "FollowUpCleared",
                        CreatedAt           = _clock.UtcNow
                    });
                }
                return updated;
            }
        }

        public async Task<bool> ResolveFollowUpAsync(Guid id)
        {
            var updated = await _repo.ResolveFollowUpAsync(id);
            if (updated)
            {
                await _repo.LogActivityAsync(new TaskActivity
                {
                    Id                  = Guid.NewGuid(),
                    TaskId              = id,
                    PerformedByUserId   = _tenant.RequiredUserId,
                    PerformedByUsername = _tenant.Username,
                    ActivityType        = "FollowUpResolved",
                    CreatedAt           = _clock.UtcNow
                });
            }
            return updated;
        }

        public async Task<bool> ChangeVisibilityAsync(Guid id, string visibility)
        {
            var valid = new[] { "Personal", "Organisation" };
            if (!valid.Contains(visibility))
                throw new ArgumentException("Invalid visibility.");

            var existing = await _repo.GetByIdAsync(id)
                ?? throw new ArgumentException("Task not found.");

            var updated = await _repo.ChangeVisibilityAsync(id, visibility);
            if (updated)
            {
                await _repo.LogActivityAsync(new TaskActivity
                {
                    Id                  = Guid.NewGuid(),
                    TaskId              = id,
                    PerformedByUserId   = _tenant.RequiredUserId,
                    PerformedByUsername = _tenant.Username,
                    ActivityType        = "VisibilityChanged",
                    OldValue            = existing.Visibility,
                    NewValue            = visibility,
                    CreatedAt           = _clock.UtcNow
                });
            }
            return updated;
        }


        public async Task<bool> ChangePriorityAsync(Guid id, string priority)
        {
            var valid = new[] { "High", "Medium", "Low" };
            if (!valid.Contains(priority))
                throw new ArgumentException("Invalid priority value.");

            var existing = await _repo.GetByIdAsync(id)
                ?? throw new ArgumentException("Task not found.");

            var updated = await _repo.ChangePriorityAsync(id, priority);
            if (updated && existing.Priority != priority)
            {
                await _repo.LogActivityAsync(new TaskActivity
                {
                    Id                  = Guid.NewGuid(),
                    TaskId              = id,
                    PerformedByUserId   = _tenant.RequiredUserId,
                    PerformedByUsername = _tenant.Username,
                    ActivityType        = "PriorityChanged",
                    OldValue            = existing.Priority,
                    NewValue            = priority,
                    CreatedAt           = _clock.UtcNow
                });
            }
            return updated;
        }

        public async Task AddCommentAsync(Guid id, string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
                throw new ArgumentException("Comment cannot be empty.");

            var existing = await _repo.GetByIdAsync(id)
                ?? throw new ArgumentException("Task not found.");

            await _repo.LogActivityAsync(new TaskActivity
            {
                Id                  = Guid.NewGuid(),
                TaskId              = id,
                PerformedByUserId   = _tenant.RequiredUserId,
                PerformedByUsername = _tenant.Username,
                ActivityType        = "CommentAdded",
                Comment             = comment.Trim(),
                CreatedAt           = _clock.UtcNow
            });
        }

        public async Task<bool> DeleteAsync(Guid id)
            => await _repo.DeleteAsync(id);

        // scope "my": private order, only the current user ever sees it.
        // scope "team": shared order, every org member sees the same result.
        public async Task ReorderAsync(string scope, List<string> orderedTaskIds)
        {
            var validScopes = new[] { "my", "team" };
            if (!validScopes.Contains(scope))
                throw new ArgumentException("Invalid scope.");
            if (orderedTaskIds is null || orderedTaskIds.Count == 0)
                throw new ArgumentException("orderedTaskIds is required.");

            List<Guid> ids;
            try { ids = orderedTaskIds.Select(Guid.Parse).ToList(); }
            catch (FormatException) { throw new ArgumentException("orderedTaskIds must be valid GUIDs."); }

            var scopeType = scope == "team" ? "Team" : "User";
            var scopeId = scope == "team" ? _tenant.RequiredOrgId : _tenant.RequiredUserId;

            var valid = await _repo.FilterValidTaskIdsAsync(ids, requireOrgVisible: scope == "team");
            var filtered = ids.Where(valid.Contains).ToList();
            if (filtered.Count == 0) return;

            await _repo.ReorderAsync(scopeType, scopeId, filtered);
        }

        public Task<(int TodayCount, int OverdueCount)> GetSummaryCountsAsync()
            => _repo.GetSummaryCountsAsync();
    }
}
