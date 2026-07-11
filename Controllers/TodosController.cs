using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.DTOs;
using Tasked.Entities;
using Tasked.Enums;
using Tasked.Services;

namespace Tasked.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectService _auth;

    public TodosController(ApplicationDbContext db, ProjectService projectService)
    {
        _db = db;
        _auth = projectService;
    }

    [HttpPost("projects/{projectId}")]
    [Authorize]
    public async Task<IActionResult> CreateTodo(Guid projectId, [FromQuery] TodoRequest request)
    {
        if(!Enum.IsDefined(request.Status)) return BadRequest("Invalid status");
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var requesterId = User.GetUserId();

        var membership = await _db.ProjectMembers
            .Where(m => m.UserId == requesterId && m.ProjectId == projectId)
            .Include(m => m.User)
            .Include(m=> m.Project)
            .SingleOrDefaultAsync();

        if(membership is null || !_auth.CanContribute(membership)) return Forbid();

        var todo = new Todo
        {
            ProjectId = projectId,
            Title = request.Title,
            Description = request.Description,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow,
            CreatedById = requesterId,
            IssueNo = membership.Project.IssueCount + 1
        };

        membership.Project.IssueCount++;

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
            ProjectName = membership.Project.Name,
            Title = todo.Title,
            Description = todo.Description,
            Status = todo.Status,
            CreatedAt = todo.CreatedAt,
            IssueNo = todo.IssueNo,
            CreatedBy = todo.CreatedById,
            CreatedByName = membership.User.Username
        };

        return CreatedAtAction(
            nameof(GetTodo), 
            new { todoId = todo.Id }, 
            dto
        );
    }

    [HttpGet("projects/{projectId}")]
    public  async Task<IActionResult> GetProjectTodos(Guid projectId, [FromQuery] GetManyTodosRequest request)
    {   
        var requesterId = User.GetNullableUserId();
        var parent = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .FirstOrDefaultAsync();

        if(parent is null) return NotFound();

        if(!await _auth.CanView(parent, requesterId)) return NotFound();

        var query = _db.Todos
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId);

        if(!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(t => t.Title.Contains(request.Search) || (!string.IsNullOrWhiteSpace(t.Description) && t.Description.Contains(request.Search)));
        
        if(request.Status is not null && Enum.IsDefined((TodoStatus) request.Status)) query = query.Where(t => t.Status == request.Status);
        
        if(request.Assigned is not null) query = query.Where(t => t.AssignedId == request.Assigned);
        
        query = query
            .Include(t => t.Assigned)
            .Include(t => t.CreatedBy);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var todos = await query
            .OrderBy(t => t.IssueNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => 
                new TodoDto()
                {
                    Id = t.Id,
                    ProjectId = t.ProjectId,
                    ProjectName = t.Project.Name,
                    Title = t.Title,
                    Description = t.Description,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    Assigned = t.AssignedId,
                    AssignedName = t.Assigned == null ? null : t.Assigned.Username,
                    IssueNo = t.IssueNo,
                    CreatedBy = t.CreatedById,
                    CreatedByName = t.CreatedBy == null ? null : t.CreatedBy.Username
                }).ToListAsync();

        return Ok(todos);
    }

    [HttpDelete("{todoId}")]
    [Authorize]
    public async Task<IActionResult> DeleteTodo(Guid todoId)
    {
        var requesterId = User.GetUserId();
        
        var todo = await _db.Todos
            .Where(t => t.Id == todoId)
            .SingleOrDefaultAsync();

        if(todo is null) return NotFound();

        var membership = await _db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.UserId == requesterId && m.ProjectId == todo.ProjectId)
            .SingleOrDefaultAsync();

        if(membership is null || (todo.CreatedById != requesterId && membership.Role != MemberRole.Owner && membership.Role != MemberRole.Admin)) return Forbid();
        
        _db.Todos.Remove(todo);
        
        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }
        
        return NoContent();
    }

    [HttpGet("{todoId}")]
    public async Task<IActionResult> GetTodo(Guid todoId)
    {
        var requesterId = User.GetNullableUserId();
        
        var todo = await _db.Todos
            .AsNoTracking()
            .Where(t => t.Id == todoId)
            .Select(t => 
                new TodoDto()
                {
                    Id = t.Id,
                    ProjectId = t.ProjectId,
                    ProjectName = t.Project.Name,
                    Title = t.Title,
                    Description = t.Description,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    Assigned = t.AssignedId,
                    IssueNo = t.IssueNo,
                    CreatedBy = t.CreatedById,
                    CreatedByName = t.CreatedBy == null ? null : t.CreatedBy.Username
                }).SingleOrDefaultAsync();

        if(todo is null) return NotFound();
        
        var parent = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == todo.ProjectId)
            .FirstOrDefaultAsync();

        if(parent is null) return StatusCode(500);

        if(!await _auth.CanView(parent, requesterId)) return NotFound();

        return Ok(todo);
    }

    [HttpPatch("{todoId}/assign/{userId}")]
    [Authorize]
    //Make DTO?
    public async Task<IActionResult> AssignTodo(Guid todoId, Guid userId)
    {
        var requesterId = User.GetUserId();

        var todo = await _db.Todos
            .Where(t => t.Id == todoId)
            .Include(t => t.CreatedBy)
            .SingleOrDefaultAsync();

        if(todo is null) return NotFound("Task not found");

        var membership = await _db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.ProjectId == todo.ProjectId)
            .Include(m => m.User)
            .Include(m=> m.Project)
            .SingleOrDefaultAsync();

        if(membership is null) return NotFound("Cannot assign a task to a user that is not a member of the project");

        if(!_auth.CanContribute(membership)) return Conflict("User must be a contributor or higher to be assigned to tasks");

        if(requesterId != userId)
        {
            var admin = await _auth.AdminPermissions(membership.Project, requesterId);
            if(!admin) return Forbid();
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
            ProjectName = membership.Project.Name,
            Title = todo.Title,
            Description = todo.Description,
            Status = todo.Status,
            CreatedAt = todo.CreatedAt,
            Assigned = todo.AssignedId,
            AssignedName = membership.User.Username,
            IssueNo = todo.IssueNo,
            CreatedBy = todo.CreatedById,
            CreatedByName = todo.CreatedBy?.Username
        };

        return Ok(dto);
    }

    //update status of todo 
    [HttpPatch("{todoId}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateTodoStatus(Guid todoId, TodoStatus status)
     {
        if(!Enum.IsDefined(status)) return BadRequest("Invalid status");

        var requesterId = User.GetUserId();
        
        var todo = await _db.Todos
            .Where(t => t.Id == todoId)
            .Include(t => t.CreatedBy)
            .Include(t => t.Assigned)
            .SingleOrDefaultAsync();

        if(todo is null) return NotFound("Task not found");

        if(status == todo.Status) return NoContent();

        var membership = await _db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.UserId == requesterId && m.ProjectId == todo.ProjectId)
            .Include(m => m.User)
            .Include(m=> m.Project)
            .SingleOrDefaultAsync();

        if(membership is null || !_auth.CanContribute(membership)) return Forbid();

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
            ProjectName = membership.Project.Name,
            Title = todo.Title,
            Description = todo.Description,
            Status = todo.Status,
            CreatedAt = todo.CreatedAt,
            Assigned = todo.AssignedId,
            AssignedName = todo.Assigned?.Username,
            IssueNo = todo.IssueNo,
            CreatedBy = todo.CreatedById,
            CreatedByName = todo.CreatedBy?.Username
        };

        return Ok(dto);

    }

    [HttpPatch("{todoId}")]
    [Authorize]
    public async Task<IActionResult> UpdateTodo(Guid todoId, [FromQuery] TodoUpdateRequest request)
    {
        var requesterId = User.GetUserId();

        if((request.Title is null || request.Title == "") && request.Description is null && (request.Status is null || !Enum.IsDefined((TodoStatus) request.Status)) && request.Assigned is null && !request.Unassign)
        {
            return BadRequest("Must update at least one field.");
        }

        var todo = await _db.Todos
            .Where(t => t.Id == todoId)
            .Include(t => t.Assigned)
            .Include(t => t.CreatedBy)
            .Include(t => t.Project)
            .SingleOrDefaultAsync();

        if(todo is null) return NotFound();

        var membership = await _db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.UserId == requesterId && m.ProjectId == todo.ProjectId)
            .SingleOrDefaultAsync();

        if(membership is null || !_auth.CanContribute(membership)) return Forbid();
        
        if(request.Title != "") todo.Title = request.Title ?? todo.Title;        

        todo.Description = request.Description ?? todo.Description;

        if(request.Status is not null && Enum.IsDefined((TodoStatus) request.Status)) todo.Status = (TodoStatus) request.Status;

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
            ProjectName = todo.Project.Name,
            Title = todo.Title,
            Description = todo.Description,
            Status = todo.Status,
            CreatedAt = todo.CreatedAt,
            Assigned = todo.AssignedId,
            AssignedName = todo.Assigned?.Username,
            IssueNo = todo.IssueNo,
            CreatedBy = todo.CreatedById,
            CreatedByName = todo.CreatedBy?.Username
        };

        return Ok(dto);
    }

}
