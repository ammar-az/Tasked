using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.Models;

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

    //create task
    [HttpPost("projects/{projectId}")]
    public async Task<IActionResult> CreateTodo(Guid projectId, string title)
    {
        var todo = new Todo
        {
            ProjectId = projectId,
            Title = title,
            Status = Enums.TodoStatus.Backlog,
            CreatedAt = DateTime.UtcNow
        };

        _db.Todos.Add(todo);
        await _db.SaveChangesAsync();
        return Ok(todo);
    }

    //get all tasks in a project
    [HttpGet("projects/{projectId}")]
    public  async Task<IActionResult> GetProjectTodos(Guid projectId)
    {   
        var todos = await _db.Todos
        .Where(todo => todo.ProjectId == projectId)
        .ToListAsync();

        return Ok(todos);
    }

    [HttpGet("{todoId}")]
    public async Task<IActionResult> GetTodo(Guid todoId)
    {
        var todo = await _db.Todos
        .Where(t => t.Id == todoId)
        .ExecuteDeleteAsync();

        return Ok(todo);
    }

    [HttpDelete("{todoId}")]
    public async Task<IActionResult> DeleteTodo(Guid todoId)
    {
        var todo = await _db.Todos
        .FindAsync(todoId);

        return Ok(todo);
    }

    //patch

}