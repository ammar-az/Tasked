using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.DTOs;
using Tasked.Entities;
using Tasked.Enums;
using Tasked.Jwt;

namespace Tasked.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodosController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TodosController(ApplicationDbContext db)
    {
        _db = db;
    }

    //all of these should be checking for visibility at least
    //create task, should check user is a contributor or higher in project to do so
    [HttpPost("projects/{projectId}")]
    //DTO here
    public async Task<IActionResult> CreateTodo(TodoRequest request, Guid projectId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var project = await _db.Projects.Where(p => p.Id == projectId).SingleOrDefaultAsync();
        if(project == null)
        {
            return NotFound();
        }

        var todo = new Todo
        {
            ProjectId = projectId,
            Title = request.Title,
            Description = request.Description,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow,
            IssueNo = project.IssueCount + 1,
        };
        project.IssueCount++;

        _db.Todos.Add(todo);

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch(DbUpdateException e)
        {
            await transaction.RollbackAsync();
            return Conflict(e.InnerException?.Message);
        }

        var dto = new TodoDto()
        {
            Id = todo.Id,
            ProjectId = todo.ProjectId,
            Title = todo.Title,
            Description = todo.Description,
            Status = todo.Status,
            CreatedAt = todo.CreatedAt,
            Assigned = todo.AssignedId,
            IssueNo = todo.IssueNo
        };

        return CreatedAtAction(
            nameof(GetTodo), 
            new { todoId = todo.Id }, 
            dto
        );
    }

    [HttpGet("projects/{projectId}")]
    [Authorize]
    public  async Task<IActionResult> GetProjectTodos(Guid projectId)
    {   
        var requesterId = User.GetUserId();
        // vis check

        var todos = await _db.Todos
        .AsNoTracking()
        .Where(todo => todo.ProjectId == projectId)
        .Select(t => 
            new TodoDto()
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                Assigned = t.AssignedId,
                IssueNo = t.IssueNo
            }).ToListAsync();

        return Ok(todos);
    }

    [HttpGet("projects/{projectId}/assigned/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetProjectAssigned(Guid projectId, Guid userId)
    {
        var requesterId = User.GetUserId();
        //vis check

        var membership = await _db.ProjectMembers
        .Where(m => m.UserId == userId && m.ProjectId == projectId)
        .SingleOrDefaultAsync();

        if(membership == null)
        {
            return NotFound("User is not a member of the project");
        }

        var todos = await _db.Todos
        .AsNoTracking()
        .Where(todo => todo.ProjectId == projectId && todo.AssignedId == userId)
        .Select(t => 
            new TodoDto()
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                Assigned = t.AssignedId,
                IssueNo = t.IssueNo
            }).ToListAsync();

        return Ok(todos);
    }

    //anyone can archive a todo, can anyone delete?
    [HttpDelete("{todoId}")]
    [Authorize]
    public async Task<IActionResult> DeleteTodo(Guid todoId)
    {
        var userId = User.GetUserId();
        // permission check
        
        var deleted = await _db.Todos
        .Where(t => t.Id == todoId)
        .ExecuteDeleteAsync();

        if(deleted == 0)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("{todoId}")]
    [Authorize]
    public async Task<IActionResult> GetTodo(Guid todoId)
    {
        var userId = User.GetUserId();
        //vis check
        
        var todo = await _db.Todos
        .AsNoTracking()
        .Where(t => t.Id == todoId)
        .Select(t => 
            new TodoDto()
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                Assigned = t.AssignedId,
                IssueNo = t.IssueNo
            }).SingleOrDefaultAsync();

        if(todo == null)
        {
            return NotFound();
        }

        return Ok(todo);
    }

    //assign user to a todo
    [HttpPatch("{todoId}/assign/{userId}")]
    [Authorize]
    //Make DTO?
    public async Task<IActionResult> AssignTodo(Guid todoId, Guid userId)
    {
        var requesterId = User.GetUserId();
        // permission check

        var todo = await _db.Todos
        .Where(t => t.Id == todoId)
        .SingleOrDefaultAsync();

        if(todo == null)
        {
            return NotFound("Task not found");
        }

        var user = await _db.Users
        .Where(u => u.Id == userId)
        .SingleOrDefaultAsync();

        var membership = await _db.ProjectMembers
        .Where(m => m.UserId == userId && m.ProjectId == todo.ProjectId)
        .SingleOrDefaultAsync();

        if(membership == null)
        {
            return NotFound("Cannot assign a task to a user that is not a member of the project");
        }

        if(membership.Role == MemberRole.Viewer)
        {
            return Conflict("User must be a contributor or higher to be assigned to tasks");
        }

        todo.AssignedId = userId;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

        var dto = new TodoDto()
        {
            Id = todo.Id,
            ProjectId = todo.ProjectId,
            Title = todo.Title,
            Description = todo.Description,
            Status = todo.Status,
            CreatedAt = todo.CreatedAt,
            Assigned = todo.AssignedId,
            IssueNo = todo.IssueNo
        };

        return Ok(dto);
    }

    //update status of todo 
    [HttpPatch("{todoId}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateTodoStatus(Guid todoId, TodoStatus status)
     {
        var requesterId = User.GetUserId();
        // permission check

        if(!Enum.IsDefined(status))
        {
            return BadRequest("Invalid status");
        }

        var todo = await _db.Todos
        .Where(t => t.Id == todoId)
        .SingleOrDefaultAsync();

        if(todo == null)
        {
            return NotFound("Task not found");
        }

        if(status == todo.Status)
        {
            return NoContent();
        }

        todo.Status = status;

        if(status == TodoStatus.Archived || status == TodoStatus.Completed)
        {
            todo.AssignedId = null;
            todo.Assigned = null;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

        var dto = new TodoDto()
        {
            Id = todo.Id,
            ProjectId = todo.ProjectId,
            Title = todo.Title,
            Description = todo.Description,
            Status = todo.Status,
            CreatedAt = todo.CreatedAt,
            Assigned = todo.AssignedId,
            IssueNo = todo.IssueNo
        };

        return Ok(dto);

    }

    //patch Title, description, maybe timestamp to bump
    [HttpPatch("{todoId}")]
    [Authorize]
    //DTO here
    public async Task<IActionResult> UpdateTodo(TodoUpdateRequest request, Guid todoId)
    {

        var userId = User.GetUserId();
        //permission check

        if((request.Title == null || request.Title == "") && request.Description == null && request.Status == null && request.Assigned == null && !request.Unassign)
        {
            return BadRequest("Must update at least one field.");
        }

        var todo = await _db.Todos
        .Where(t => t.Id == todoId)
        .SingleOrDefaultAsync();

        if(todo == null)
        {
            return NotFound("Task not found");
        }
        
        if(request.Title != "")
        {
            todo.Title = request.Title ?? todo.Title;        
        }

        todo.Description = request.Description ?? todo.Description;
        todo.Status = request.Status ?? todo.Status;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }
        var dto = new TodoDto()
        {
            Id = todo.Id,
            ProjectId = todo.ProjectId,
            Title = todo.Title,
            Description = todo.Description,
            Status = todo.Status,
            CreatedAt = todo.CreatedAt,
            Assigned = todo.AssignedId,
            IssueNo = todo.IssueNo
        };

            return Ok(dto);
    }

}
