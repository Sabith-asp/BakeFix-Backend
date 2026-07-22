using BakeFix.Models;
using BakeFix.Repositories;

namespace BakeFix.Services
{
    public class TaskService
    {
        private readonly TaskRepository _repo;
        private readonly ITenantContext _tenant;

        public TaskService(TaskRepository repo, ITenantContext tenant)
        {
            _repo   = repo;
            _tenant = tenant;
        }

        public Task<IEnumerable<TaskItem>> GetMyTasksAsync(
            string view, string? status, string? priority, string? category, string? search)
            => _repo.GetMyTasksAsync(view, status, priority, category, search);

        public Task<IEnumerable<TaskItem>> GetTeamTasksAsync(
            string view, string? status, string? priority, string? category, string? search)
            => _repo.GetTeamTasksAsync(view, status, priority, category, search);

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
                CreatedAt           = DateTime.UtcNow,
                UpdatedAt           = DateTime.UtcNow
            };

            await _repo.CreateAsync(task);
            await _repo.LogActivityAsync(new TaskActivity
            {
                Id                  = Guid.NewGuid(),
                TaskId              = task.Id,
                PerformedByUserId   = _tenant.RequiredUserId,
                PerformedByUsername = _tenant.Username,
                ActivityType        = "Created",
                CreatedAt           = DateTime.UtcNow
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
                UpdatedAt          = DateTime.UtcNow
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
                    CreatedAt           = DateTime.UtcNow
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

            DateTime? completedAt = status == "Completed" ? DateTime.UtcNow : null;
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
                    CreatedAt           = DateTime.UtcNow
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
                    CreatedAt           = DateTime.UtcNow
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
                    CreatedAt           = DateTime.UtcNow
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
                    CreatedAt           = DateTime.UtcNow
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
                CreatedAt           = DateTime.UtcNow
            });
        }

        public async Task<bool> DeleteAsync(Guid id)
            => await _repo.DeleteAsync(id);

        public Task<(int TodayCount, int OverdueCount)> GetSummaryCountsAsync()
            => _repo.GetSummaryCountsAsync();
    }
}
