using System.Text.Json.Serialization;
using BakeFix.Services.Json;

namespace BakeFix.Models
{
    public class TaskItem
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public string CreatedByUsername { get; set; } = "";
        public Guid? AssignedToUserId { get; set; }
        public string? AssignedToUsername { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Category { get; set; } = "";
        public string Priority { get; set; } = "Medium";
        public string Status { get; set; } = "Pending";
        public string Visibility { get; set; } = "Personal";
        public DateTime OriginalTargetDate { get; set; }
        public DateTime CurrentTargetDate { get; set; }

        [JsonConverter(typeof(NullableUtcInstantJsonConverter))]
        public DateTime? CompletedAt { get; set; }
        public bool NeedsFollowUp { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string? FollowUpNotes { get; set; }

        [JsonConverter(typeof(NullableUtcInstantJsonConverter))]
        public DateTime? FollowUpResolvedAt { get; set; }

        [JsonConverter(typeof(NullableUtcInstantJsonConverter))]
        public DateTime? DeletedAt { get; set; }

        [JsonConverter(typeof(UtcInstantJsonConverter))]
        public DateTime CreatedAt { get; set; }

        [JsonConverter(typeof(UtcInstantJsonConverter))]
        public DateTime UpdatedAt { get; set; }
        public int? SortOrder { get; set; }
        public List<TaskActivity>? ActivityLog { get; set; }
    }

    public class TaskActivity
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public Guid PerformedByUserId { get; set; }
        public string PerformedByUsername { get; set; } = "";
        public string ActivityType { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Comment { get; set; }

        [JsonConverter(typeof(UtcInstantJsonConverter))]
        public DateTime CreatedAt { get; set; }
    }

    public class DailyNote
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public string CreatedByUsername { get; set; } = "";
        public DateTime NoteDate { get; set; }
        public string? Title { get; set; }
        public string Content { get; set; } = "";
        public string Visibility { get; set; } = "Personal";

        [JsonConverter(typeof(UtcInstantJsonConverter))]
        public DateTime CreatedAt { get; set; }

        [JsonConverter(typeof(UtcInstantJsonConverter))]
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateTaskRequest
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Category { get; set; } = "";
        public string Priority { get; set; } = "Medium";
        public string Visibility { get; set; } = "Personal";
        public string CurrentTargetDate { get; set; } = "";
        public string? AssignedToUserId { get; set; }
        public string? AssignedToUsername { get; set; }
    }

    public class UpdateTaskRequest
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Category { get; set; } = "";
        public string Priority { get; set; } = "Medium";
        public string? AssignedToUserId { get; set; }
        public string? AssignedToUsername { get; set; }
    }

    public class ChangeStatusRequest
    {
        public string Status { get; set; } = "";
    }

    public class ChangeDateRequest
    {
        public string NewDate { get; set; } = "";
    }

    public class ChangeVisibilityRequest
    {
        public string Visibility { get; set; } = "Personal";
    }


    public class ChangePriorityRequest
    {
        public string Priority { get; set; } = "Medium";
    }

    // scope: "my" (private order, visible only to the current user) or "team" (shared org-wide order)
    public class ReorderTasksRequest
    {
        public string Scope { get; set; } = "my";
        public List<string> OrderedTaskIds { get; set; } = new();
    }

    // Setting needsFollowUp=true requires followUpDate; set needsFollowUp=false (or omit it while
    // already false) to clear the flag. Resolving is a separate action (see ResolveFollowUp).
    public class SetFollowUpRequest
    {
        public bool NeedsFollowUp { get; set; }
        public string? FollowUpDate { get; set; }
        public string? FollowUpNotes { get; set; }
    }

    public class AddCommentRequest
    {
        public string Comment { get; set; } = "";
    }

    public class UpsertNoteRequest
    {
        public string Content { get; set; } = "";
        public string Visibility { get; set; } = "Personal";
    }
}
