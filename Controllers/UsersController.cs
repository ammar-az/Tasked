using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.Models;

namespace Tasked.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
     private readonly ApplicationDbContext _db;

    public UsersController(ApplicationDbContext db)
    {
        _db = db;
    }

    //register new user
    [HttpPost]
    public async Task<IActionResult> Register(string username, string email)
    {
        var user = new User
        {
            Username = username,
            Email = email
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(user);
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserById(Guid userId)
    {
        var user = await _db.Users
        .FindAsync(userId);

        return Ok(user);
    }
    
    [HttpDelete("{userId}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var user = await _db.Users
        .Where(u => u.Id == userId)
        .ExecuteDeleteAsync();

        return Ok(user);
    }


    //get all projects a user owns 
    [HttpGet("{userId}/projects")]
    public async Task<IActionResult> GetUserProjects(Guid userId)
    {   
        var projects = await _db.Projects
        .Where(project => project.OwnerId == userId)
        .ToListAsync();

        return Ok(projects);
    }
    
    //patch

}