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
    [HttpPost]
    public async Task<IActionResult> CreateTodo(Guid projectId, string title)
    {
        var todo = new Todo
        {
            ProjectId = projectId,
            Title = title
        };

        _db.Todos.Add(todo);
        await _db.SaveChangesAsync();
        return Ok(todo);
    }

    //get all tasks in a project
    [HttpGet("{projectId}")]
    public  async Task<IActionResult> FetchProjectTodos(Guid projectId)
    {   
        var todos = await _db.Todos
        .Where(todo => todo.ProjectId == projectId)
        .ToListAsync();

        return Ok(todos);
    }

    //delete a task

    //modify a task 

    //resolve a task (falls under modify?)

}