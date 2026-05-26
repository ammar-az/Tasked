using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.DTOs;
using Tasked.Entities;

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
        await _db.SaveChangesAsync();

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

    //should be able to update owner, name, description, and visiblity

    // [HttpPatch("{projectId}")]
    // public async Task<IActionResult> UpdateProject(Guid projectId)
    // {
    //     var project = await _db.Projects
    //     .Where(p => p.Id == projectId)
    //     .ExecuteUpdateAsync();

    //     return Ok(project);
    // }

    //add member to project
    [HttpPost("{projectId}/members")]
    public async Task<IActionResult> NewMembership(Guid projectId, Guid userId)
    {
        var membership = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = Enums.MemberRole.Contributor
        };

        _db.ProjectMembers.Add(membership);
        await _db.SaveChangesAsync();

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

        if(member.Role == Enums.MemberRole.Owner)
        {
            return Conflict("Cannot leave a project you own. Transfer ownership or delete the project.");
        }
        
        _db.ProjectMembers.Remove(member.self);
        await _db.SaveChangesAsync();

        return NoContent();
    }


    //Only owner can promote to admin, admin can switch viewer to contributor though

    // [HttpPatch("{projectId}/members/{userId}")]
    // public async Task<IActionResult> ChangeRole(Guid projectId, Guid userId)
    // {
    //     //need roles done to get this working
    //     return Ok(member);
    // }

}
