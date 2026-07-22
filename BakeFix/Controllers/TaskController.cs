using BakeFix.Filters;
using BakeFix.Models;
using BakeFix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("tasks")]
    [Authorize]
    [RequireModule("Tasks")]
    public class TaskController : ControllerBase
    {
        private readonly TaskService _taskService;
        private readonly DailyNoteService _noteService;

        public TaskController(TaskService taskService, DailyNoteService noteService)
        {
            _taskService = taskService;
            _noteService = noteService;
        }

        // GET /tasks/my?view=today&status=&priority=&category=&search=
        [HttpGet("my")]
        public async Task<IActionResult> GetMyTasks(
            [FromQuery] string view = "today",
            [FromQuery] string? status = null,
            [FromQuery] string? priority = null,
            [FromQuery] string? category = null,
            [FromQuery] string? search = null)
            => Ok(await _taskService.GetMyTasksAsync(view, status, priority, category, search));

        // GET /tasks/team?view=today&status=&priority=&category=&search=
        [HttpGet("team")]
        public async Task<IActionResult> GetTeamTasks(
            [FromQuery] string view = "today",
            [FromQuery] string? status = null,
            [FromQuery] string? priority = null,
            [FromQuery] string? category = null,
            [FromQuery] string? search = null)
            => Ok(await _taskService.GetTeamTasksAsync(view, status, priority, category, search));

        // GET /tasks/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var (todayCount, overdueCount) = await _taskService.GetSummaryCountsAsync();
            return Ok(new { todayCount, overdueCount });
        }

        // GET /tasks/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var task = await _taskService.GetByIdAsync(id);
            if (task is null) return NotFound(new { message = "Task not found." });
            return Ok(task);
        }

        // POST /tasks
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
        {
            var task = await _taskService.CreateAsync(request);
            return Ok(task);
        }

        // PUT /tasks/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest request)
        {
            var updated = await _taskService.UpdateAsync(id, request);
            if (!updated) return NotFound(new { message = "Task not found or access denied." });
            return NoContent();
        }

        // DELETE /tasks/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _taskService.DeleteAsync(id);
            if (!deleted) return NotFound(new { message = "Task not found or access denied." });
            return NoContent();
        }

        // PATCH /tasks/{id}/status
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeStatusRequest request)
        {
            var updated = await _taskService.ChangeStatusAsync(id, request.Status);
            if (!updated) return NotFound(new { message = "Task not found or access denied." });
            return NoContent();
        }

        // PATCH /tasks/{id}/target-date
        [HttpPatch("{id:guid}/target-date")]
        public async Task<IActionResult> ChangeDate(Guid id, [FromBody] ChangeDateRequest request)
        {
            var updated = await _taskService.ChangeDateAsync(id, request.NewDate);
            if (!updated) return NotFound(new { message = "Task not found or access denied." });
            return NoContent();
        }

        // PATCH /tasks/{id}/visibility
        [HttpPatch("{id:guid}/visibility")]
        public async Task<IActionResult> ChangeVisibility(Guid id, [FromBody] ChangeVisibilityRequest request)
        {
            var updated = await _taskService.ChangeVisibilityAsync(id, request.Visibility);
            if (!updated) return NotFound(new { message = "Task not found or access denied." });
            return NoContent();
        }


        // PATCH /tasks/{id}/priority
        [HttpPatch("{id:guid}/priority")]
        public async Task<IActionResult> ChangePriority(Guid id, [FromBody] ChangePriorityRequest request)
        {
            var updated = await _taskService.ChangePriorityAsync(id, request.Priority);
            if (!updated) return NotFound(new { message = "Task not found or access denied." });
            return NoContent();
        }

        // POST /tasks/{id}/comments
        [HttpPost("{id:guid}/comments")]
        public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentRequest request)
        {
            await _taskService.AddCommentAsync(id, request.Comment);
            return NoContent();
        }

        // ── Daily Notes ──────────────────────────────────────────────────────

        // GET /tasks/notes/{date}?includeOrg=true
        [HttpGet("notes/{date}")]
        public async Task<IActionResult> GetNotes(string date, [FromQuery] bool includeOrg = true)
        {
            if (!DateTime.TryParse(date, out var parsedDate))
                return BadRequest(new { message = "Invalid date format." });

            var personal = await _noteService.GetPersonalByDateAsync(parsedDate);
            var orgNotes = includeOrg
                ? (await _noteService.GetOrgNotesByDateAsync(parsedDate)).ToList()
                : new List<DailyNote>();

            return Ok(new { personal, orgNotes });
        }

        // GET /tasks/notes/recent
        [HttpGet("notes/recent")]
        public async Task<IActionResult> GetRecentNotes([FromQuery] int limit = 7)
            => Ok(await _noteService.GetRecentPersonalAsync(limit));

        // PUT /tasks/notes/{date}
        [HttpPut("notes/{date}")]
        public async Task<IActionResult> UpsertNote(string date, [FromBody] UpsertNoteRequest request)
        {
            var note = await _noteService.UpsertAsync(date, request);
            return Ok(note);
        }

        // DELETE /tasks/notes/{date}
        [HttpDelete("notes/{date}")]
        public async Task<IActionResult> DeleteNote(string date)
        {
            if (!DateTime.TryParse(date, out var parsedDate))
                return BadRequest(new { message = "Invalid date format." });
            await _noteService.DeletePersonalByDateAsync(parsedDate);
            return NoContent();
        }
    }
}
