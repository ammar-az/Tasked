using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.Entities;
using Tasked.DTOs;
using Microsoft.AspNetCore.Authorization;
using Tasked.Services;
using Tasked.Enums;
using Azure;

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
    //Remove for Auth? 
    // [HttpPost]
    // public async Task<ActionResult<UserDto>> Register(string username, string email)
    // {
    //     var user = new User
    //     {
    //         Username = username,
    //         Email = email
    //     };

    //     _db.Users.Add(user);
    //     try
    //     {
    //         await _db.SaveChangesAsync();
    //     }
    //     catch(DbUpdateException e)
    //     {
    //         return Conflict(e.InnerException?.Message);
    //     }

    //     var dto = new UserDto()
    //     {
    //         Id = user.Id,
    //         Username = user.Username,
    //         OrgId = user.OrgId,
    //         OrgName = user.Org?.Name,
    //         Email = user.Email
    //     };

    //     return CreatedAtAction(
    //         nameof(GetUserById), 
    //         new { userId = user.Id }, 
    //         dto
    //     );
    // }

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

        if(user is null) return NotFound();

        return Ok(user);
    }

    [HttpDelete]
    [Authorize]
    public async Task<ActionResult> DeleteUser()
    {
        var userId = User.GetUserId();

        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => 
                new
                {
                    self = u,
                    owns = u.OwnedProjects.Any()
                }).SingleOrDefaultAsync();

        if(user is null) return NotFound();
        

        if(user.owns) return Conflict("Cannot delete an account with active projects. Transfer ownership or delete projects first.");

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


    // //get all projects a user owns 
    // [HttpGet("{userId}/projects")]
    // [Authorize]
    // public async Task<ActionResult<IEnumerable<ProjectDto>>> GetUserProjects(Guid userId)
    // {   
    //     var requesterId = User.GetUserId();
    //     //Vis check, skipped if self
        
    //     var projects = await _db.Projects
    //     .AsNoTracking()
    //     .Where(p => p.OwnerId == userId)
    //     .Select(p => 
    //         new ProjectDto
    //         {
    //             Id = p.Id,
    //             OwnerId = p.OwnerId,
    //             OwnerName = p.Owner.Username,
    //             Name = p.Name,
    //             Description = p.Description,
    //             OrgId = p.OrgId,
    //             OrgName = p.Org == null ? null : p.Org.Name,
    //             CreatedAt = p.CreatedAt,
    //             IsVisible = p.IsVisible,
    //             JoinPolicy = p.JoinPolicy
    //         }).ToListAsync();

    //     return Ok(projects);
    // }

    // [HttpGet("{userId}/memberof")]
    // [Authorize]
    // public async Task<ActionResult<IEnumerable<ProjectDto>>> GetUserMembership(Guid userId)
    // {   
    //     var requesterId = User.GetUserId();
    //     //Vis check

    //     var memberships = await _db.ProjectMembers
    //     .AsNoTracking()
    //     .Where(membership => membership.UserId == userId)
    //     .Select(m => 
    //     new ProjectDto
    //         {
    //             Id = m.Project.Id,
    //             OwnerId = m.Project.OwnerId,
    //             Name = m.Project.Name,
    //             OwnerName = m.Project.Owner.Username,
    //             Description = m.Project.Description,
    //             OrgId = m.Project.OrgId,
    //             OrgName = m.Project.Org == null ? null : m.Project.Org.Name,
    //             JoinPolicy = m.Project.JoinPolicy,
    //             CreatedAt = m.Project.CreatedAt,
    //             IsVisible = m.Project.IsVisible
    //         }).ToListAsync();

    //     return Ok(memberships);
    // }

    [HttpGet("{userId}/projects")]
    public async Task<ActionResult> GetUserProjects(Guid userId , [FromQuery] MemberOverviewRequest request)
    {
        var requesterId = User.GetNullableUserId();

        var requester = requesterId is null 
            ? null 
            : await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == requesterId)
                .Select(u =>
                    new
                    {
                        u.OrgId,
                    }
                )
                .FirstOrDefaultAsync();

        //implements own visibility check
        var query = _db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Where(m => 
                m.Project.IsVisible ||
                    (requester != null && 
                        ((requester.OrgId != null && requester.OrgId == m.Project.OrgId)
                        ||
                        m.Project.Members.Any(pm => pm.UserId == requesterId && pm.Role != MemberRole.Banned)) 
                    )
                );

        if(request.Role is not null) query = query.Where(m => m.Role == request.Role);
        else query = query.Where(m => m.Role != MemberRole.Banned);

        if(request.Owner) query = query.Where(m => m.Role == MemberRole.Owner);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var memberships = await query
        .OrderBy(m => m.Project.Name)
        .ThenBy(m => m.ProjectId)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(m =>
            new MemberOverviewDto()
            {
                ProjectId = m.ProjectId,
                ProjectName = m.Project.Name,
                ProjectDesc = m.Project.Description,
                Role = m.Role,
                OrgId = m.Project.OrgId,
                OrgName = m.Project.Org == null ? null : m.Project.Org.Name
            }).ToListAsync();

        return Ok(memberships);
    }

    [HttpPatch]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateUser(UserUpdateRequest request)
    {
        var username = request.Username;
        var email = request.Email;

        if(username is null && email is null) return BadRequest("No fields to update");
    
        var userId = User.GetUserId();
        
        if(username == "") return BadRequest("Invalid username");

        //this can actually check for valid email addresses instead eventually
        if(email == "") return BadRequest("Invalid email");

        var user = await _db.Users
            .Where(u => u.Id == userId)
            .SingleOrDefaultAsync();

        if(user is null)return NotFound();

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

    [HttpGet("todos")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<TodoDto>>> GetUserTodos([FromQuery] GetManyTodosRequest request)
    {
        var userId = User.GetUserId();

        var query = _db.Todos
            .AsNoTracking()
            .Where(t => t.AssignedId == userId);
        
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        
        var todos = await query
            .OrderBy(t => t.ProjectId)
            .ThenBy(t => t.IssueNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => 
                new TodoDto
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
                }).ToListAsync();

        return Ok(todos);
    }
}
