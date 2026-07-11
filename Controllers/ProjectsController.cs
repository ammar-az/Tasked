using Microsoft.AspNetCore.Authorization;
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
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectService _auth;

    public ProjectsController(ApplicationDbContext db, ProjectService projectService)
    {
        _db = db;
        _auth = projectService;
    }

    //create project | add org, description, visibility 
    [HttpPost]
    [Authorize]
    //request dto here
    public async Task<IActionResult> CreateProject(ProjectRequest request)
    {
        var userId = User.GetUserId();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrgId = request.OrgId,
            OwnerId = userId,
            Name = request.Name,
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
            OwnerName = project.Owner.Username,
            Name = project.Name,
            Description = project.Description,
            OrgId = project.OrgId,
            OrgName = project.Org?.Name,
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

    //Should only succeed if public, private but member of org, or private but member
    [HttpGet("{projectId}")]
    public async Task<IActionResult> GetProject(Guid projectId)
    {

        var requesterId = User.GetNullableUserId();

        var p = await _db.Projects
        .AsNoTracking()
        .Where(p => p.Id == projectId)
        .Include(p => p.Owner)
        .SingleOrDefaultAsync();

        if(p == null)
        {
            return NotFound();
        }  

        if(!await _auth.CanView(p, requesterId))
        {
            return NotFound();
        } 

        var dto = new ProjectDto()
        {
            Id = p.Id,
            OwnerId = p.OwnerId,
            OwnerName = p.Owner.Username,
            Name = p.Name,
            Description = p.Description,
            OrgId = p.OrgId,
            IsVisible = p.IsVisible,
            JoinPolicy = p.JoinPolicy,
            CreatedAt = p.CreatedAt
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

        if(deleted == 0)
        {
            return Forbid();
        }

        return NoContent();
    }

    //add member to project
    [HttpPost("{projectId}/members")]
    [Authorize]
    public async Task<IActionResult> NewMembership(Guid projectId)
    {
        var userId = User.GetUserId();

        var membership = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = MemberRole.Contributor
        };

        _db.ProjectMembers.Add(membership);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

        var dto = new MemberDto()
        {
            ProjectId = membership.ProjectId,
            UserId = membership.UserId,
            Username = User.Identity?.Name ?? "",
            ProjectName = "you forgot to check project name :)",
            Role = membership.Role,
            JoinTime = membership.JoinTime
        };

        return CreatedAtAction(
            nameof(GetMembers), 
            new { projectId }, 
            dto
        );
    }

    //get all members of a project, same visible check as before, 
    [HttpGet("{projectId}/members")]
    public async Task<IActionResult> GetMembers(Guid projectId, [FromQuery] MemberOverviewRequest request)
    {   
        var requesterId = User.GetUserId();
        
        var project = await _db.Projects
        .AsNoTracking()
        .Where(p => p.Id == projectId)
        .FirstOrDefaultAsync();

        if(project == null) return NotFound();

        if(!await _auth.CanView(project, requesterId)) return NotFound();

        var query = _db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId);

        if(request.Role != null) query = query.Where(m => m.Role == request.Role);
        else query = query.Where(m => m.Role != MemberRole.Banned);

        if(!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(m => m.User.Username.Contains(request.Search));

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var members = await query
        .OrderBy(m => m.Role)
        .ThenBy(m => m.JoinTime)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(m => 
            new MemberDto()
            {
                UserId = m.UserId,
                Username = m.User.Username,
                ProjectId = m.ProjectId,
                ProjectName = m.Project.Name,
                Role = m.Role,
                JoinTime = m.JoinTime
            }).ToListAsync();

        return Ok(members);
    }

    //only an admin or owner can remove users other than themselves. Must handle case where owner leaves
    //auth check might be: if issuer wants to remove themselves, allow if not the owner. If issuer wants to remove someone else, check if permitted
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

        if(member == null)
        {
            return NotFound();
        }

        if(member.Role == MemberRole.Owner)
        {
            return Conflict("Cannot leave a project you own. Transfer ownership or delete the project.");
        }

        var todos = await _db.Todos
        .Where(todo => todo.ProjectId == projectId && todo.AssignedId == userId)
        .ToListAsync();

        foreach(var todo in todos)
        {
            todo.AssignedId = null;
        }

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

    [HttpPatch("{projectId}/bans/{userId}")]
    [Authorize]
    public async Task<IActionResult> BanMember(Guid projectId, Guid userId)
    {
        var issuerId = User.GetUserId();

        var permitted = await _auth.AdminPermissions(projectId, issuerId);

        if(!permitted) return Forbid();
        

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

        if(member == null)
        {
            return NotFound("No such membership");
        }

        if(member.Role == MemberRole.Owner)
        {
            return Conflict("Cannot ban owner, ownership must be transferred");
        }

        //permissions check here: owner: any, admin: contributor and below only, other: immediately reject

        member.self.Role = MemberRole.Banned;

        var todos = await _db.Todos
        .Where(todo => todo.ProjectId == projectId && todo.AssignedId == userId)
        .ToListAsync();

        foreach(var todo in todos)
        {
            todo.AssignedId = null;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

        var dto = new MemberDto()
        {
            UserId = member.UserId,
            Username = member.Username,
            ProjectId = member.ProjectId,
            ProjectName = member.ProjectName,
            JoinTime = DateTime.Now,
            Role = MemberRole.Banned
        };

        return Ok(dto);
    }

    [HttpPatch("EditProject")]
    [Authorize]
    public async Task<IActionResult> EditProject(ProjectDto request)
    {
        var userId = User.GetUserId();

        var project = await _db.Projects
        .Where(p => p.Id == request.Id)
        .SingleOrDefaultAsync();

        if(project == null)
        {
            return NotFound();
        }

        //permissions check here
        //this will check for admins too later
        if(!_auth.OwnsProject(project, userId))
            return Forbid();
        

        if(request.Name != "")
        {
            project.Name = request.Name ?? project.Name;
        }

        project.Description = request.Description ?? project.Description;
        project.IsVisible = request.IsVisible;

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
            OwnerName = project.Owner.Username,
            Name = project.Name,
            Description = project.Description,
            OrgId = project.OrgId,
            OrgName = project.Org?.Name,
            IsVisible = project.IsVisible,
            JoinPolicy = project.JoinPolicy,
            CreatedAt = project.CreatedAt
        };

        return Ok(dto);
    }

    [HttpPatch("{projectId}/members/{userId}")]
    [Authorize]
    //DTO here?
    public async Task<IActionResult> ChangeRole(Guid projectId, Guid userId, MemberRole newRole)
    {   
        if(newRole == MemberRole.Owner)
        {
            return Conflict("Promotion to owner not possible.");
        }

        //permissions check here: owner: any, admin: contributor and below only, other: immediately reject

        var member = await _db.ProjectMembers
        .Where(m => m.ProjectId == projectId && m.UserId == userId)
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

        if(member == null)
        {
            return NotFound("No such membership");
        }

        if(member.Role == MemberRole.Owner)
        {
            return Conflict("Cannot demote owner, ownership must be transferred");
        }

        if(member.Role == newRole)
        {
            return NoContent();
        }

        member.self.Role = newRole;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

        var dto = new MemberDto()
        {
            UserId = member.UserId,
            Username = member.Username,
            ProjectId = member.ProjectId,
            ProjectName = member.ProjectName,
            JoinTime = member.JoinTime,
            Role = newRole
        };

        return Ok(dto);
    }

    [HttpPatch("{projectId}/org/")]
    [Authorize]
    public async Task<IActionResult> ChangeToOrg(Guid projectId)
    {
        //Only owner can do this
        var userId = User.GetUserId();

        var project = await _db.Projects
            .Where(p => p.Id == projectId)
            .Include(p => p.Owner)
            .ThenInclude(o => o.Org)
            .SingleOrDefaultAsync();

        if(project == null)
        {
            return NotFound();
        }

        if (!_auth.OwnsProject(project, userId))
            return Forbid();

        if(project.OrgId != null)
        {
            return NoContent();
        }
        
        if(project.Owner.OrgId == null)
        {
            return Conflict("Project owner must belong to an organization to move project to their organization.");
        }
        

        project.OrgId = project.Owner.OrgId;

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
            OwnerName = project.Owner.Username,
            Name = project.Name,
            Description = project.Description,
            OrgId = project.OrgId,
            OrgName = project.Org?.Name,
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
        //Only owner can do this
        var userId = User.GetUserId();
        var project = await _db.Projects
            .Where(p => p.Id == projectId)
            .SingleOrDefaultAsync();

        if(project == null)
        {
            return NotFound();
        }

        if(project.OrgId == null)
        {
            return NoContent();
        }

        if (!_auth.OwnsProject(project, userId))
            return Forbid();

        project.OrgId = null;

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
            OwnerName = project.Owner.Username,
            Name = project.Name,
            Description = project.Description,
            OrgId = project.OrgId,
            OrgName = project.Org?.Name,
            CreatedAt = project.CreatedAt,
            IsVisible = project.IsVisible,
            JoinPolicy = project.JoinPolicy
        };

        return Ok(dto);
    }

    [HttpPatch("{projectId}/transfer")]
    [Authorize]
    //DTO here?
    public async Task<IActionResult> TransferOwnership(Guid projectId, Guid newOwnerId)
    {
        //Only owner can do this
        var userId = User.GetUserId();

        var project = await _db.Projects
        .Where(p => p.Id == projectId)
        .Include(p => p.Org)
        .SingleOrDefaultAsync();

        if(project == null)
        {
            return NotFound();
        }

        if (!_auth.OwnsProject(project, userId))
            return Forbid();

        if(project.OwnerId == newOwnerId)
        {
            return NoContent();
        }

        var membership = await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == newOwnerId)
            .Include(m => m.User)
            .SingleOrDefaultAsync();

        if(membership == null)
        {
            return Conflict("Cannot transfer ownership to a user that is not a member of the project.");
        }

        if(project.OrgId != null && project.OrgId != membership.User.OrgId)
        {
            return Conflict("Projects associated with an organization can only transfer ownership to users within the same organization.");
        }

        var oldOwnerMembership = await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == project.OwnerId)
            .SingleOrDefaultAsync();

        if(oldOwnerMembership == null)
        {
            return Conflict("Current owner not recognized as member of the project. Critical data integrity issue, this should never happen.");
        }

        project.OwnerId = newOwnerId;
        oldOwnerMembership.Role = MemberRole.Admin;
        membership.Role = MemberRole.Owner;

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
            OwnerName = project.Owner.Username,
            Name = project.Name,
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
