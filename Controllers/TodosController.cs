using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.DTOs;
using Tasked.Entities;

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
    public async Task<IActionResult> CreateTodo(Guid projectId, string title, string? description)
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
            Title = title,
            Description = description,
            Status = Enums.TodoStatus.Backlog,
            CreatedAt = DateTime.UtcNow,
            IssueNo = project.IssueCount + 1,
        };
        project.IssueCount++;

        _db.Todos.Add(todo);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

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
    public  async Task<IActionResult> GetProjectTodos(Guid projectId)
    {   
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

    //anyone can archive a todo, can anyone delete?
    [HttpDelete("{todoId}")]
    public async Task<IActionResult> DeleteTodo(Guid todoId)
    {
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
    public async Task<IActionResult> GetTodo(Guid todoId)
    {
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

    //update status of todo (only if assigned or open?)

    //patch Title, description, maybe timestamp to bump

}
