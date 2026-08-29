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

    [HttpGet("{username}")]
    public async Task<ActionResult<UserDto>> GetUserByName(string username)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Username == username)
            .Select(u => 
                new UserDto()
                {
                    Id = u.Id,
                    Username = u.Username,
                    OrgId = u.OrgId,
                    OrgName = u.Org == null ? null : u.Org.Name,
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
        catch(DbUpdateException)
        {
            return Conflict("An error occurred while trying to delete this user account");
        }

        return NoContent();
    }

    [HttpPatch]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateUser(UserUpdateRequest request)
    {
        var username = request.Username;

        if(username is null) return BadRequest("No fields to update");
    
        var userId = User.GetUserId();
        
        if(username == "") return BadRequest("Invalid username");

        var user = await _db.Users
            .Where(u => u.Id == userId)
            .SingleOrDefaultAsync();

        if(user is null)return NotFound();

        user.Username = username ?? user.Username;
        _db.Users.Update(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException)
        {
            return Conflict("An error occured while trying to update account details");
        }
        
        var dto = new UserDto()
        {
            Id = user.Id,
            Username = user.Username,
            OrgId = user.OrgId,
            OrgName = user.Org?.Name,
        };

        return Ok(dto);
    }

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

        var query = _db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId);
        
        //implements own visibility check, but skips if user is querying their own info
        if(requesterId != userId)
        {
            query = query.Where(m => 
                m.Project.IsVisible ||
                    (requester != null && 
                        ((requester.OrgId != null && requester.OrgId == m.Project.OrgId)
                        ||
                        m.Project.Members.Any(pm => pm.UserId == requesterId && pm.Role != MemberRole.Banned)) 
                    )
                );
        }

        if (request.RoleMin)
        {
            if(request.Role == MemberRole.Contributor) query = query.Where(m => m.Role == MemberRole.Contributor || m.Role == MemberRole.Admin || m.Role == MemberRole.Owner);
            else if(request.Role == MemberRole.Admin) query = query.Where(m => m.Role == MemberRole.Admin || m.Role == MemberRole.Owner);
            else if(request.Role is not null) query = query.Where(m => m.Role == request.Role);
            else query = query.Where(m => m.Role != MemberRole.Banned);
        }
        else
        {
            if(request.Role is not null) query = query.Where(m => m.Role == request.Role);
            else query = query.Where(m => m.Role != MemberRole.Banned);
        }

        query = request.SortBy switch
        {
            MemberSort.Name => request.Descending
                ? query.OrderByDescending(m => m.Project.Name).ThenByDescending(m => m.JoinTime)
                : query.OrderBy(m => m.Project.Name).ThenBy(m => m.JoinTime),

            MemberSort.Role => request.Descending
                ? query.OrderByDescending(m => m.Role).ThenByDescending(m => m.Project.Name).ThenByDescending(m => m.JoinTime)
                : query.OrderBy(m => m.Role).ThenBy(m => m.Project.Name).ThenBy(m => m.JoinTime),

            MemberSort.Time => request.Descending
                ? query.OrderByDescending(m => m.JoinTime).ThenByDescending(m => m.Project.Name)
                : query.OrderBy(m => m.JoinTime).ThenBy(m => m.Project.Name),

             _ => query.OrderBy(m => m.Project.Name).ThenBy(m => m.JoinTime)
        };

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var memberships = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(m =>
            new MemberOverviewDto()
            {
                ProjectId = m.ProjectId,
                ProjectName = m.Project.Name,
                ProjectSlug = m.Project.Slug,
                ProjectDesc = m.Project.Description,
                Role = m.Role,
                OrgId = m.Project.OrgId,
                OrgName = m.Project.Org == null ? null : m.Project.Org.Name
            }).ToListAsync();

        return Ok(memberships);
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
        
        query = request.SortBy switch
        {
            TodoSort.IssueNo => request.Descending 
                ? query.OrderByDescending(t => t.ProjectId).ThenByDescending(t => t.IssueNo)
                : query.OrderBy(t => t.ProjectId).ThenBy(t => t.IssueNo),

            TodoSort.Title => request.Descending 
                ? query.OrderByDescending(t => t.ProjectId).ThenByDescending(t => t.Title)
                : query.OrderBy(t => t.ProjectId).ThenBy(t => t.Title),

            TodoSort.Status => request.Descending 
                ? query.OrderByDescending(t => t.Status).ThenByDescending(t => t.ProjectId).ThenByDescending(t => t.IssueNo)
                : query.OrderBy(t => t.Status).ThenBy(t => t.ProjectId).ThenBy(t => t.IssueNo),

            // TodoSort.CreatedBy => request.Descending
            // ? query.OrderByDescending(t => t.CreatedBy).ThenByDescending(t => t.IssueNo)
            // : query.OrderBy(t => t.CreatedBy).ThenBy(t => t.IssueNo),

            _ => query.OrderBy(t => t.ProjectId).ThenBy(t => t.IssueNo)
        };

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
                    ProjectSlug = t.Project.Slug,
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
