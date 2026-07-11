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

    [HttpPost]
    [Authorize]
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
            IsVisible = request.IsVisible ?? true,
            JoinPolicy = request.JoinPolicy ?? JoinPolicy.Open,
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

    [HttpGet("{projectId}")]
    public async Task<ActionResult<ProjectDto>> GetProject(Guid projectId)
    {
        var requesterId = User.GetNullableUserId();

        var p = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Include(p => p.Owner)
            .SingleOrDefaultAsync();

        if(p is null) return NotFound();

        if(!await _auth.CanView(p, requesterId)) return NotFound();

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

        if(deleted == 0) return Forbid();

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
    public async Task<ActionResult<IEnumerable<MemberDto>>> GetMembers(Guid projectId, [FromQuery] MemberOverviewRequest request)
    {   
        var requesterId = User.GetNullableUserId();
        
        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .FirstOrDefaultAsync();

        if(project is null) return NotFound();

        if(!await _auth.CanView(project, requesterId)) return NotFound();

        var query = _db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId);

        if(request.Role is not null && Enum.IsDefined((MemberRole) request.Role)) query = query.Where(m => m.Role == request.Role);
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

    [HttpPatch("{projectId}/bans/{userId}")]
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

        if(member.Role == MemberRole.Owner) return Conflict("Cannot ban owner, ownership must be transferred");

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

    [HttpPatch("{projectId}")]
    [Authorize]
    public async Task<IActionResult> EditProject(Guid projectId, [FromQuery] ProjectRequest request)
    {
        var userId = User.GetUserId();

        var project = await _db.Projects
            .Where(p => p.Id == projectId)
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

    [HttpPatch("{projectId}/members/")]
    [Authorize]
    public async Task<IActionResult> ChangeRole(Guid projectId, [FromQuery] MemberRoleChangeRequest request)
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

        member.self.Role = request.Role;

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
    public async Task<IActionResult> TransferOwnership(Guid projectId, Guid newOwnerId)
    {
        var userId = User.GetUserId();

        var project = await _db.Projects
            .Where(p => p.Id == projectId && p.OwnerId == userId)
            .Include(p => p.Org)
            .SingleOrDefaultAsync();

        if(project is null) return NotFound();

        if (!_auth.OwnsProject(project, userId)) return Forbid();

        if(project.OwnerId == newOwnerId) return NoContent();

        var membership = await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == newOwnerId)
            .Include(m => m.User)
            .SingleOrDefaultAsync();

        if(membership is null) return Conflict("Cannot transfer ownership to a user that is not a member of the project.");
        
        if(project.OrgId is not null && project.OrgId != membership.User.OrgId) return Conflict("Projects associated with an organization can only transfer ownership to users within the same organization.");

        var oldOwnerMembership = await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == project.OwnerId)
            .SingleOrDefaultAsync();

        if(oldOwnerMembership is null) return Conflict("Current owner not recognized as member of the project. Critical data integrity issue, this should never happen.");
        
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
