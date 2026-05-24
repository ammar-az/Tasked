using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.Entities;
using Tasked.DTOs;

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

    //should return: username, org, and email
    //this is how a proper get should look
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserDto>> GetUserById(Guid userId)
    {
    
        var user = await _db.Users
        .Where(u => u.Id == userId)
        .Select(u => 
            new UserDto()
            {
                Id = u.Id,
                Username = u.Username,
                OrgId = u.OrgId,
                OrgName = u.Org == null ? null : u.Org.Name,
                Email = u.Email
            }).SingleOrDefaultAsync();

        if(user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    //only user can delete own acc
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

    //user should be able to change their email, password, and username + leave and join orgs

}
