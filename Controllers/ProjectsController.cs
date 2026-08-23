using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.DTOs;
using Tasked.Entities;
using Tasked.Enums;
using Tasked.Services;
using System.Text.RegularExpressions;

namespace Tasked.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectService _auth;

    public ProjectsController(ApplicationDbContext db, ProjectService projectService)
    {
        _db = db;
        _auth = projectService;
    }



private async Task<string> CreateUniqueSlug(string name)
{
    var baseSlug = name
        .Trim()
        .ToLowerInvariant();

    baseSlug = Regex.Replace(baseSlug, @"[^a-z0-9]+", "-");
    baseSlug = baseSlug.Trim('-');

    if (string.IsNullOrWhiteSpace(baseSlug))
    {
        baseSlug = "project";
    }

    var slug = baseSlug;
    var number = 2;

    while (await _db.Projects.AnyAsync(p => p.Slug == slug))
    {
        slug = $"{baseSlug}-{number}";
        number++;
    }

    return slug;
}

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateProject(ProjectRequest request)
    {
        var userId = User.GetUserId();

        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Include(u => u.Org)
            .SingleOrDefaultAsync();

        if(user is null) return Conflict();
        
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrgId = request.Org ? user.OrgId : null,
            OwnerId = userId,
            Name = request.Name,
            Slug = await CreateUniqueSlug(request.Name),
            Description = request.Description,
            IsVisible = request.IsVisible,
            JoinPolicy = request.JoinPolicy,
            CreatedAt = DateTime.UtcNow
        };

        var projectMember = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = userId,
            Role = MemberRole.Owner,
            JoinTime = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        _db.ProjectMembers.Add(projectMember);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

        var dto = new ProjectDto()
        {
            Id = project.Id,
            OwnerId = project.OwnerId,
            OwnerName = user.Username,
            Name = project.Name,
            Slug = await CreateUniqueSlug(request.Name),
            Description = project.Description,
            OrgId = project.OrgId,
            OrgName = user.Org?.Name,
            IsVisible = project.IsVisible,
            JoinPolicy = project.JoinPolicy,
            CreatedAt = project.CreatedAt
        };

        return CreatedAtAction(
            nameof(GetProject), 
            new { projectId = project.Id }, 
            dto
        );
    }

    [HttpGet("{projectSlug}")]
    public async Task<ActionResult<ProjectDto>> GetProject(string projectSlug)
    {
        var requesterId = User.GetNullableUserId();

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Slug == projectSlug)
            .Include(p => p.Owner)
            .Include(p => p.Org)
            .SingleOrDefaultAsync();

        if(project is null) return NotFound();

        if(!await _auth.CanView(project, requesterId)) return NotFound();

        var dto = new ProjectDto()
        {
            Id = project.Id,
            OwnerId = project.OwnerId,
            OwnerName = project.Owner.Username,
            Name = project.Name,
            Slug = project.Slug,
            Description = project.Description,
            OrgId = project.OrgId,
            OrgName = project.Org?.Name,
            IsVisible = project.IsVisible,
            JoinPolicy = project.JoinPolicy,
            CreatedAt = project.CreatedAt
        };

        return Ok(dto);
    }

    [HttpGet("id/{projectId}")]
    public async Task<ActionResult<ProjectDto>> GetProject(Guid projectId)
    {
        var requesterId = User.GetNullableUserId();

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Include(p => p.Owner)
            .SingleOrDefaultAsync();

        if(project is null) return NotFound();

        if(!await _auth.CanView(project, requesterId)) return NotFound();

        var dto = new ProjectDto()
        {
            Id = project.Id,
            OwnerId = project.OwnerId,
            OwnerName = project.Owner.Username,
            Name = project.Name,
            Slug = project.Slug,
            Description = project.Description,
            OrgId = project.OrgId,
            IsVisible = project.IsVisible,
            JoinPolicy = project.JoinPolicy,
            CreatedAt = project.CreatedAt
        };

        return Ok(dto);
    }
    
    [HttpPatch("{projectId}")]
    [Authorize]
    public async Task<IActionResult> EditProject(Guid projectId, ProjectUpdateRequest request)
    {
        var userId = User.GetUserId();

        var project = await _db.Projects
            .Where(p => p.Id == projectId)
            .Include(p => p.Org)
            .Include(p => p.Owner)
            .SingleOrDefaultAsync();

        if(project is null) return NotFound();

        var admin = await _auth.AdminPermissions(project, userId);
        if(!admin) return Forbid();

        if(request.Name != "") project.Name = request.Name ?? project.Name;

        project.Description = request.Description ?? project.Description;
        project.IsVisible = request.IsVisible ?? project.IsVisible;
        project.JoinPolicy = request.JoinPolicy ?? project.JoinPolicy;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException)
        {
            return Conflict("An error occurred and the project could not be edited.");
        }

        var dto = new ProjectDto()
        {
            Id = project.Id,
            OwnerId = project.OwnerId,
            OwnerName = project.Owner.Username,
            Name = project.Name,
            Slug = project.Slug,
            Description = project.Description,
            OrgId = project.OrgId,
            OrgName = project.Org?.Name,
            IsVisible = project.IsVisible,
            JoinPolicy = project.JoinPolicy,
            CreatedAt = project.CreatedAt
        };

        return Ok(dto);
    }

    //Only owner can do this
    [HttpDelete("{projectId}")]
    [Authorize]
    public async Task<IActionResult> DeleteProject(Guid projectId)
    {
        var userId = User.GetUserId();
        
        var deleted = await _db.Projects
            .Where(p => p.Id == projectId && p.OwnerId == userId)
            .ExecuteDeleteAsync();

        if(deleted == 0) return Forbid();

        return NoContent();
    }

    [HttpPost("{projectId}/join")]
    [Authorize]
    public async Task<IActionResult> JoinProject(Guid projectId)
    {
        var userId = User.GetUserId();

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .SingleOrDefaultAsync();

        if(project == null) return NotFound();

        var visible = await _auth.CanView(project, userId);

        if(!visible) return NotFound();

        var permit = await _auth.CanJoin(project, userId);

        if(!permit) return Forbid();

        var preexisting = await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .SingleOrDefaultAsync();

        if(preexisting is not null)
        {
            if(preexisting.Role == MemberRole.Banned) return Forbid("You cannot join a project you have been banned from.");
            
            else if(preexisting.Role != MemberRole.Invited) return Conflict("You are already a member of this project");
        }

        var membership = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = project.JoinPolicy == JoinPolicy.ViewOnly ?  MemberRole.Viewer : MemberRole.Contributor ,
            JoinTime = DateTime.UtcNow
        };

        if (preexisting is null)
        {
            _db.ProjectMembers.Add(membership);
        }
        else
        {
            preexisting.Role = membership.Role;
            preexisting.JoinTime = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException)
        {
            return Conflict("There was an error while attempting to join the project.");
        }

        var dto = new MemberDto()
        {
            ProjectId = membership.ProjectId,
            UserId = membership.UserId,
            Username = User.Identity?.Name ?? "",
            ProjectName = project.Name,
            Role = membership.Role,
            JoinTime = membership.JoinTime
        };

        return CreatedAtAction(
            nameof(GetMembers), 
            new { projectId }, 
            dto
        );
    }

    [HttpPost("{projectId}/invite/{userId}")]
    [Authorize]
    public async Task<IActionResult> InviteToProject(Guid projectId, Guid userId)
    {
        var requesterId = User.GetUserId();

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .SingleOrDefaultAsync();

        if(project == null) return NotFound();

        var admin = await _auth.AdminPermissions(project, requesterId);

        if(!admin) return Forbid();

        var preexisting = await _db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .AnyAsync();

        if(preexisting) return Conflict("You cannot invite a user who is already a member of the project or has already been invited");

        var membership = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = MemberRole.Invited,
            JoinTime = DateTime.UtcNow
        };

        _db.ProjectMembers.Add(membership);
        
        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException)
        {
            return Conflict("There was an error while inviting the user to the project.");
        }

        var dto = new MemberDto()
        {
            ProjectId = membership.ProjectId,
            UserId = membership.UserId,
            Username = User.Identity?.Name ?? "",
            ProjectName = project.Name,
            Role = membership.Role,
            JoinTime = membership.JoinTime
        };

        return CreatedAtAction(
            nameof(GetMembers), 
            new { projectId }, 
            dto
        );
    }

    [HttpDelete("{projectId}/reject")]
    [Authorize]
    public async Task<IActionResult> RejectInvite(Guid projectId)
    {
        var userId = User.GetUserId();
        var member = await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == userId && m.Role == MemberRole.Invited)
            .SingleOrDefaultAsync();

        if(member is null) return NoContent();

        _db.ProjectMembers.Remove(member);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException)
        {
            return Conflict("There was an error while rejecting the invite.");
        }

        return NoContent();
    }

    [HttpDelete("{projectId}/leave")]
    [Authorize]
    public async Task<IActionResult> LeaveProject(Guid projectId)
    {
        var userId = User.GetUserId();
        
        var member = await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .Select(m => new 
                {
                    self = m,
                    m.ProjectId,
                    m.UserId,
                    m.Role,
                    m.Project.OwnerId
                }).SingleOrDefaultAsync();

        if(member is null) return NotFound();

        if(member.Role == MemberRole.Owner) return Conflict("Cannot leave a project you own. Transfer ownership or delete the project.");

        if(member.Role == MemberRole.Banned) return NoContent();
        
        var todos = await _db.Todos
            .Where(todo => todo.ProjectId == projectId && todo.AssignedId == userId)
            .ToListAsync();

        foreach(var todo in todos)
            todo.AssignedId = null;

        _db.ProjectMembers.Remove(member.self);
        
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

    //get all members of a project, same visible check as before, 
    [HttpGet("{projectSlug}/members")]
    public async Task<ActionResult<IEnumerable<MemberDto>>> GetMembers(string projectSlug, [FromQuery] MemberOverviewRequest request)
    {   
        var requesterId = User.GetNullableUserId();
        
        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Slug == projectSlug)
            .FirstOrDefaultAsync();

        if(project is null) return NotFound();

        if(!await _auth.CanView(project, requesterId)) return NotFound();

        var query = _db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == project.Id);

        if(request.Role is not null && Enum.IsDefined((MemberRole) request.Role)) query = query.Where(m => m.Role == request.Role);
        else query = query.Where(m => m.Role != MemberRole.Banned && m.Role != MemberRole.Invited);

        if(!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(m => m.User.Username.Contains(request.Search));

        query = request.SortBy switch
        {
            MemberSort.Name => request.Descending
                ? query.OrderByDescending(m => m.User.Username).ThenByDescending(m => m.JoinTime)
                : query.OrderBy(m => m.User.Username).ThenBy(m => m.JoinTime),

            MemberSort.Role => request.Descending
                ? query.OrderByDescending(m => m.Role).ThenByDescending(m => m.User.Username)
                : query.OrderBy(m => m.Role).ThenBy(m => m.User.Username),

            MemberSort.Time => request.Descending
                ? query.OrderByDescending(m => m.JoinTime).ThenByDescending(m => m.User.Username)
                : query.OrderBy(m => m.JoinTime).ThenBy(m => m.User.Username),

             _ => query.OrderBy(m => m.User.Username).ThenBy(m => m.JoinTime)
        };

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var members = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(m => 
            new MemberDto()
            {
                UserId = m.UserId,
                Username = m.User.Username,
                ProjectId = m.ProjectId,
                ProjectName = m.Project.Name,
                OrgId = m.User.OrgId,
                OrgName = m.User.Org == null ? null : m.User.Org.Name,
                Role = m.Role,
                JoinTime = m.JoinTime
            }).ToListAsync();

        return Ok(members);
    }

    [HttpGet("{projectSlug}/members/me")]
    public async Task<IActionResult> GetMember(string projectSlug)
    {
        var requesterId = User.GetNullableUserId();
        if (requesterId == null) return NoContent();

        var member = await _db.ProjectMembers
            .Where(m => m.Project.Slug == projectSlug && m.UserId == requesterId)
            .Select(m => 
                new MemberDto()
                {
                    UserId = m.UserId,
                    Username = m.User.Username,
                    ProjectId = m.ProjectId,
                    ProjectName = m.Project.Name,
                    Role = m.Role,
                    JoinTime = m.JoinTime
                }).SingleOrDefaultAsync();

        if(member is null) return NoContent();
    
        return Ok(member);
    }

    [HttpGet("{projectSlug}/todos")]
    public  async Task<IActionResult> GetProjectTodos(string projectSlug, [FromQuery] GetManyTodosRequest request)
    {   
        var requesterId = User.GetNullableUserId();
        var parent = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Slug == projectSlug)
            .FirstOrDefaultAsync();

        if(parent is null) return NotFound();

        if(!await _auth.CanView(parent, requesterId)) return NotFound();

        var query = _db.Todos
            .AsNoTracking()
            .Where(t => t.ProjectId == parent.Id);

        if(!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(t => t.Title.Contains(request.Search) || (!string.IsNullOrWhiteSpace(t.Description) && t.Description.Contains(request.Search)));
        
        if(request.Status is not null && Enum.IsDefined((TodoStatus) request.Status)) query = query.Where(t => t.Status == request.Status);
        
        if(request.Assigned is not null) query = query.Where(t => t.AssignedId == request.Assigned);
        
        query = query
            .Include(t => t.Assigned)
            .Include(t => t.CreatedBy);

        query = request.SortBy switch
        {
            TodoSort.IssueNo => request.Descending 
                ? query.OrderByDescending(t => t.IssueNo).ThenByDescending(t => t.Title)
                : query.OrderBy(t => t.IssueNo).ThenBy(t => t.Title),

            TodoSort.Title => request.Descending 
                ? query.OrderByDescending(t => t.Title).ThenByDescending(t => t.IssueNo)
                : query.OrderBy(t => t.Title).ThenBy(t => t.IssueNo),

            TodoSort.Status => request.Descending 
                ? query.OrderByDescending(t => t.Status).ThenByDescending(t => t.IssueNo)
                : query.OrderBy(t => t.Status).ThenBy(t => t.IssueNo),

            // TodoSort.Assigned => request.Descending
            // ? query.OrderByDescending(t => t.Assigned).ThenByDescending(t => t.IssueNo)
            // : query.OrderBy(t => t.Assigned).ThenBy(t => t.IssueNo),

            // TodoSort.CreatedBy => request.Descending
            // ? query.OrderByDescending(t => t.CreatedBy).ThenByDescending(t => t.IssueNo)
            // : query.OrderBy(t => t.CreatedBy).ThenBy(t => t.IssueNo),

            _ => query.OrderBy(t => t.IssueNo).ThenBy(t => t.Title)
        };

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var todos = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => 
                new TodoDto()
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
                    AssignedName = t.Assigned == null ? null : t.Assigned.Username,
                    IssueNo = t.IssueNo,
                    CreatedBy = t.CreatedById,
                    CreatedByName = t.CreatedBy == null ? null : t.CreatedBy.Username
                }).ToListAsync();

        return Ok(todos);
    }

    [HttpPatch("{projectId}/ban/{userId}")]
    [Authorize]
    public async Task<IActionResult> BanMember(Guid projectId, Guid userId)
    {
        var issuerId = User.GetUserId();

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .SingleOrDefaultAsync();
        
        if(project is null) return NotFound();

        var admin = await _auth.AdminPermissions(project, issuerId);

        if(!admin) return Forbid();
        
        var member = await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .Select(m => new 
                {
                    self = m,
                    m.UserId,
                    m.User.Username,
                    m.ProjectId,
                    ProjectName = m.Project.Name,
                    m.Role
                })
            .SingleOrDefaultAsync();

        if(member is null) return NotFound("No such membership");

        if(member.Role == MemberRole.Owner) return Conflict("Cannot ban owner");

        if(member.Role == MemberRole.Admin && !_auth.OwnsProject(project, issuerId)) return Forbid();

        member.self.Role = MemberRole.Banned;

        var todos = await _db.Todos
            .Where(todo => todo.ProjectId == projectId && todo.AssignedId == userId)
            .ToListAsync();

        foreach(var todo in todos)
            todo.AssignedId = null;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException)
        {
            return Conflict("An error occurred while managing the project member");
        }

        var dto = new MemberDto()
        {
            UserId = member.UserId,
            Username = member.Username,
            ProjectId = member.ProjectId,
            ProjectName = member.ProjectName,
            JoinTime = DateTime.UtcNow,
            Role = MemberRole.Banned
        };

        return Ok(dto);
    }

    [HttpPatch("{projectId}/members/")]
    [Authorize]
    public async Task<IActionResult> ChangeRole(Guid projectId, MemberRoleChangeRequest request)
    {   
        if(!Enum.IsDefined(request.Role)) return BadRequest("Invalid Role");

        if(request.Role == MemberRole.Owner) return Conflict("Transferring ownership must be done through the dedicated endpoint");
        
        if(request.Role == MemberRole.Banned) return Conflict("Bans must be made through the ban endpoint.");

        var requesterId = User.GetUserId();

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .SingleOrDefaultAsync();
        
        if(project is null) return NotFound();

        var admin = await _auth.AdminPermissions(project, requesterId);

        if(!admin) return Forbid();

        var member = await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == request.User)
            .Select(m => new 
                {
                    self = m,
                    m.UserId,
                    m.User.Username,
                    m.ProjectId,
                    ProjectName = m.Project.Name,
                    m.Role,
                    m.JoinTime
                })
            .SingleOrDefaultAsync();

        if(member is null) return NotFound("No such membership");

        if(member.Role == MemberRole.Owner) return Conflict("Cannot demote owner, ownership must be transferred");

        if(member.Role == request.Role) return NoContent();

        if((member.Role == MemberRole.Admin || request.Role == MemberRole.Admin) && !_auth.OwnsProject(project, requesterId)) return Forbid();

        if(request.Role == MemberRole.Viewer)
        {
            var todos = await _db.Todos
                .Where(todo => todo.ProjectId == projectId && todo.AssignedId == request.User)
                .ToListAsync();

            foreach(var todo in todos)
                todo.AssignedId = null;
        }

        member.self.Role = request.Role;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException)
        {
            return Conflict("An error occurred while managing the project member");
        }

        var dto = new MemberDto()
        {
            UserId = member.UserId,
            Username = member.Username,
            ProjectId = member.ProjectId,
            ProjectName = member.ProjectName,
            JoinTime = member.JoinTime,
            Role = request.Role
        };

        return Ok(dto);
    }

    [HttpPatch("{projectId}/org/")]
    [Authorize]
    public async Task<IActionResult> ChangeToOrg(Guid projectId)
    {
        var userId = User.GetUserId();

        var project = await _db.Projects
            .Where(p => p.Id == projectId && p.OwnerId == userId)
            .Include(p => p.Owner)
            .ThenInclude(o => o.Org)
            .SingleOrDefaultAsync();

        if(project is null) return NotFound();

        if(!_auth.OwnsProject(project, userId)) return Forbid();

        if(project.OrgId is not null) return NoContent();
        
        if(project.Owner.OrgId is null || project.Owner.Org is null) return Conflict("Project owner must belong to an organization to move project to their organization.");
        
        
        project.OrgId = project.Owner.OrgId;
        project.Org = project.Owner.Org;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException)
        {
            return Conflict("An error occurred while trying to update the project");
        }

        var dto = new ProjectDto()
        {
            Id = project.Id,
            OwnerId = project.OwnerId,
            OwnerName = project.Owner.Username,
            Name = project.Name,
            Slug = project.Slug,
            Description = project.Description,
            OrgId = project.OrgId,
            OrgName = project.Org.Name,
            CreatedAt = project.CreatedAt,
            IsVisible = project.IsVisible,
            JoinPolicy = project.JoinPolicy
        };

        return Ok(dto);
    }

    [HttpPatch("{projectId}/org/remove")]
    [Authorize]
    public async Task<IActionResult> RemoveFromOrg(Guid projectId)
    {
        var userId = User.GetUserId();

        var project = await _db.Projects
            .Where(p => p.Id == projectId && p.OwnerId == userId)
            .SingleOrDefaultAsync();

        if(project is null) return NotFound();

        if(project.OrgId is null) return NoContent();

        if (!_auth.OwnsProject(project, userId)) return Forbid();

        project.OrgId = null;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException)
        {
            return Conflict("An error occurred while trying to update the project");
        }

        var dto = new ProjectDto()
        {
            Id = project.Id,
            OwnerId = project.OwnerId,
            OwnerName = "I forgor :skull:",
            Name = project.Name,
            Slug = project.Slug,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            IsVisible = project.IsVisible,
            JoinPolicy = project.JoinPolicy
        };
        
        return Ok(dto);
    }

    [HttpPatch("{projectId}/transfer/{userId}")]
    [Authorize]
    public async Task<IActionResult> TransferOwnership(Guid projectId, Guid userId)
    {
        var requesterId = User.GetUserId();

        var project = await _db.Projects
            .Where(p => p.Id == projectId && p.OwnerId == requesterId)
            .Include(p => p.Org)
            .SingleOrDefaultAsync();

        if(project is null) return NotFound();

        if (!_auth.OwnsProject(project, requesterId)) return Forbid();

        if(project.OwnerId == userId) return NoContent();

        var membership = await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .Include(m => m.User)
            .SingleOrDefaultAsync();

        if(membership is null) return Conflict("Cannot transfer ownership to a user that is not a member of the project.");
        
        if(membership.Role == MemberRole.Banned) return Conflict("Cannot transfer ownership to a banned user");

        if(project.OrgId is not null && project.OrgId != membership.User.OrgId) return Conflict("Projects associated with an organization can only transfer ownership to users within the same organization.");

        var oldOwnerMembership = await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == project.OwnerId)
            .SingleOrDefaultAsync();

        if(oldOwnerMembership is null) return Conflict("Current owner not recognized as member of the project. Critical data integrity issue, this should never happen.");
        
        project.OwnerId = userId;
        oldOwnerMembership.Role = MemberRole.Admin;
        membership.Role = MemberRole.Owner;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException)
        {
            return Conflict("An error occurred while trying to update the project");
        }

        var dto = new ProjectDto()
        {
            Id = project.Id,
            OwnerId = project.OwnerId,
            OwnerName = membership.User.Username,
            Name = project.Name,
            Slug = project.Slug,
            Description = project.Description,
            OrgId = project.OrgId,
            OrgName = project.Org?.Name,
            CreatedAt = project.CreatedAt,
            IsVisible = project.IsVisible,
            JoinPolicy = project.JoinPolicy
        };

        return Ok(dto);
    }

}
