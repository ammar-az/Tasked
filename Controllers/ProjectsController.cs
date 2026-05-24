using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
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
    public async Task<IActionResult> CreateProject(Guid userId, string name)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Name = name,
            IsVisible = true
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
        return Ok(project);
    }

    //Should only succeed if public, private but member of org, or private but member
    [HttpGet("{projectId}")]
    public async Task<IActionResult> GetProject(Guid projectId)
    {
        var project = await _db.Projects
        .FindAsync(projectId);

        return Ok(project);
    }
    //Only owner can do this
    [HttpDelete("{projectId}")]
    public async Task<IActionResult> DeleteProject(Guid projectId)
    {
        var project = await _db.Projects
        .Where(p => p.Id == projectId)
        .ExecuteDeleteAsync();

        return Ok(project);
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

    //get all projects a user is a member of | either check visibility or make different route for that
    [HttpGet("{userId}/memberof")]
    public  async Task<IActionResult> GetUserMembership(Guid userId)
    {   
        var memberships = await _db.ProjectMembers
        .Where(membership => membership.UserId == userId)
        .Include(membership => membership.Project)
        .ToListAsync();

        return Ok(memberships);
    }
    //get all members of a project, same visible check as before, 
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
        return Ok(membership);
    }

    //get all members of a project, same visible check as before, 
    [HttpGet("{projectId}/members")]
        public async Task<IActionResult> GetMembers(Guid projectId)
    {   
        var members = await _db.ProjectMembers
        .Where(member => member.ProjectId == projectId)
        .Include(member => member.User)
        .ToListAsync();

        return Ok(members);
    }

    //only an admin or owner can remove users other than themselves. Must handle case where owner leaves
    [HttpDelete("{projectId}/members/{userId}")]
    public async Task<IActionResult> LeaveProject(Guid projectId, Guid userId)
    {
        var member = await _db.ProjectMembers
        .Where(m => m.ProjectId == projectId && m.UserId == userId)
        .ExecuteDeleteAsync();

        return Ok(member);
    }


    //Only owner can promote to admin, admin can switch viewer to contributor though

    // [HttpPatch("{projectId}/members/{userId}")]
    // public async Task<IActionResult> ChangeRole(Guid projectId, Guid userId)
    // {
    //     //need roles done to get this working
    //     return Ok(member);
    // }

}
