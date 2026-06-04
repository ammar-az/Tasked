using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.DTOs;
using Tasked.Entities;
using Tasked.Enums;

namespace Tasked.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ProjectsController(ApplicationDbContext db)
    {
        _db = db;
    }

    //create project | add org, description, visibility 
    [HttpPost]
    public async Task<IActionResult> CreateProject(Guid userId, Guid? orgId, string name, string? description, bool? isVisible)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            OwnerId = userId,
            Name = name,
            Description = description,
            IsVisible = isVisible ?? true
        };

        var projectMember = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = userId,
            Role = Enums.MemberRole.Owner
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
            Name = project.Name,
            Description = project.Description,
            OrgId = project.OrgId
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
        var project = await _db.Projects
        .AsNoTracking()
        .Where(p => p.Id == projectId)
        .Select(p => 
            new ProjectDto()
            {
                Id = p.Id,
                OwnerId = p.OwnerId,
                Name = p.Name,
                Description = p.Description,
                OrgId = p.OrgId
            }).SingleOrDefaultAsync();

        if(project == null)
        {
            return NotFound();
        }

        return Ok(project);
    }
    
    //Only owner can do this
    [HttpDelete("{projectId}")]
    public async Task<IActionResult> DeleteProject(Guid projectId)
    {
        var deleted = await _db.Projects
        .Where(p => p.Id == projectId)
        .ExecuteDeleteAsync();

        if(deleted == 0)
        {
            return NotFound();
        }

        return NoContent();
    }

    //add member to project
    [HttpPost("{projectId}/members")]
    public async Task<IActionResult> NewMembership(Guid projectId, Guid userId)
    {
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
            Role = membership.Role
        };

        return CreatedAtAction(
            nameof(GetMembers), 
            new { projectId }, 
            dto
        );
    }

    //get all members of a project, same visible check as before, 
    [HttpGet("{projectId}/members")]
        public async Task<IActionResult> GetMembers(Guid projectId)
    {   
        var members = await _db.ProjectMembers
        .AsNoTracking()
        .Where(member => member.ProjectId == projectId)
        .Select(m => 
            new MemberDto()
            {
                UserId = m.UserId,
                Username = m.User.Username,
                ProjectId = m.ProjectId,
                ProjectName = m.Project.Name,
                Role = m.Role
            }).ToListAsync();

        return Ok(members);
    }

    //only an admin or owner can remove users other than themselves. Must handle case where owner leaves
    //auth check might be: if issuer wants to remove themselves, allow if not the owner. If issuer wants to remove someone else, check if permitted
    [HttpDelete("{projectId}/members/{userId}")]
    public async Task<IActionResult> LeaveProject(Guid projectId, Guid userId)
    {
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
        //no permissions check done yet, only template to ensure only own account can leave
        //this check doesnt work yet as userID refers to user leaving, issuer will be passed through authcontext when implemented
        if(member.UserId != userId)
        {
            return Forbid();
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

    [HttpPatch("{projectId}")]
    public async Task<IActionResult> EditProject(Guid projectId, string? name, string? description, bool? isVisible)
    {
        var project = await _db.Projects
        .Where(p => p.Id == projectId)
        .SingleOrDefaultAsync();

        if(project == null)
        {
            return NotFound();
        }

        //permissions check here

        if(name != "")
        {
            project.Name = name ?? project.Name;
        }

        project.Description = description ?? project.Description;
        project.IsVisible = isVisible ?? project.IsVisible;

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
            Name = project.Name,
            Description = project.Description,
            OrgId = project.OrgId
        };

        return Ok(dto);
    }

    [HttpPatch("{projectId}/members/{userId}")]
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
                m.Role
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
            Role = newRole
        };

        return Ok(dto);
    }

    [HttpPatch("{projectId}/org/")]
    public async Task<IActionResult> ChangeToOrg(Guid projectId)
    {
        //Only owner can do this

        var project = await _db.Projects
            .Where(p => p.Id == projectId)
            .Include(p => p.Owner)
            .ThenInclude(o => o.Org)
            .SingleOrDefaultAsync();

        if(project == null)
        {
            return NotFound();
        }

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
            Name = project.Name,
            Description = project.Description,
            OrgId = project.OrgId,
            OrgName = project.Org?.Name
        };

        return Ok(dto);
    }

    [HttpPatch("{projectId}/org/remove")]
    public async Task<IActionResult> RemoveFromOrg(Guid projectId)
    {
        //Only owner can do this

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
            Name = project.Name,
            Description = project.Description,
            OrgId = null,
            OrgName = null
        };

        return Ok(dto);
    }

    [HttpPatch("{projectId}/transfer")]
    public async Task<IActionResult> TransferOwnership(Guid projectId, Guid newOwnerId)
    {
        //Only owner can do this

        var project = await _db.Projects
        .Where(p => p.Id == projectId)
        .Include(p => p.Org)
        .SingleOrDefaultAsync();

        if(project == null)
        {
            return NotFound();
        }

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
            Name = project.Name,
            Description = project.Description,
            OrgId = project.OrgId,
            OrgName = project.Org?.Name
        };

        return Ok(dto);
    }
}
