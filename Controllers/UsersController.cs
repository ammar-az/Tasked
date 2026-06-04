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
    //this is how proper POST should look
    [HttpPost]
    public async Task<ActionResult<UserDto>> Register(string username, string email)
    {
        var user = new User
        {
            Username = username,
            Email = email
        };

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

        var dto = new UserDto()
        {
            Id = user.Id,
            Username = user.Username,
            OrgId = user.OrgId,
            OrgName = user.Org?.Name,
            Email = user.Email
        };

        return CreatedAtAction(
            nameof(GetUserById), 
            new { userId = user.Id }, 
            dto
        );
    }

    //should return: username, org, and email
    //this is how a proper GET should look
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserDto>> GetUserById(Guid userId)
    {
        var user = await _db.Users
        .AsNoTracking()
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
    public async Task<ActionResult> DeleteUser(Guid userId)
    {
        var user = await _db.Users
        .Where(u => u.Id == userId)
        .Select(u => 
            new
            {
                self = u,
                owns = u.OwnedProjects.Any()
            }).SingleOrDefaultAsync();

        if(user == null)
        {
            return NotFound();
        }

        if(user.owns)
        {
            return Conflict("Cannot delete an account with active projects. Transfer ownership or delete projects first.");
        }

        _db.Users.Remove(user.self);
        
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


    //get all projects a user owns 
    [HttpGet("{userId}/projects")]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetUserProjects(Guid userId)
    {   
        var projects = await _db.Projects
        .AsNoTracking()
        .Where(p => p.OwnerId == userId)
        .Select(p => 
            new ProjectDto
            {
                Id = p.Id,
                OwnerId = p.OwnerId,
                Name = p.Name,
                Description = p.Description,
                OrgId = p.OrgId,
                OrgName = p.Org == null ? null : p.Org.Name
            }).ToListAsync();

        return Ok(projects);
    }

    //get all projects a user is a member of | either check visibility or make different route for that
    //might need to be a memberdto rather than projectdto since we might need the role in the project as well
    [HttpGet("{userId}/memberof")]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetUserMembership(Guid userId)
    {   
        var memberships = await _db.ProjectMembers
        .AsNoTracking()
        .Where(membership => membership.UserId == userId)
        .Select(m => 
        new ProjectDto
            {
                Id = m.Project.Id,
                OwnerId = m.Project.OwnerId,
                Name = m.Project.Name,
                Description = m.Project.Description,
                OrgId = m.Project.OrgId,
                OrgName = m.Project.Org == null ? null : m.Project.Org.Name
            }).ToListAsync();

        return Ok(memberships);
    }

    //user should be able to change their email, password, and username + leave and join orgs

    [HttpPatch("{userId}")]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid userId, string? username, string? email)
    {
        if(username == "")
        {
            return BadRequest("Invalid username");
        }

        //this can actually check for valid email addresses instead eventually
        if(email == "")
        {
            return BadRequest("Invalid email");
        }

        var user = await _db.Users
        .Where(u => u.Id == userId)
        .SingleOrDefaultAsync();

        if(user == null)
        {
            return NotFound();
        }

        user.Username = username ?? user.Username;
        user.Email = email ?? user.Email;
        _db.Users.Update(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }
        
        var dto = new UserDto()
        {
            Id = user.Id,
            Username = user.Username,
            OrgId = user.OrgId,
            OrgName = user.Org?.Name,
            Email = user.Email
        };

        return Ok(dto);
    }

    [HttpGet("{userId}/todos")]
    public async Task<ActionResult<IEnumerable<TodoDto>>> GetUserTodos(Guid userId)
    {
        var todos = await _db.Todos
        .AsNoTracking()
        .Where(t => t.AssignedId == userId)
        .Select(t => 
            new TodoDto
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
}
