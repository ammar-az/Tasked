using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.Models;

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

    //create project
    [HttpPost]
    public async Task<IActionResult> CreateProject(Guid userId, string name)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Name = name
        };

        var projectMember = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = userId
            //role = owner/admin
        };

        _db.Projects.Add(project);
        _db.ProjectMembers.Add(projectMember);
        await _db.SaveChangesAsync();
        return Ok(project);
    }

    [HttpGet("{projectId}")]
    public async Task<IActionResult> GetProject(Guid projectId)
    {
        var project = await _db.Projects
        .FindAsync(projectId);

        return Ok(project);
    }

    [HttpDelete("{projectId}")]
    public async Task<IActionResult> DeleteProject(Guid projectId)
    {
        var project = await _db.Projects
        .Where(p => p.Id == projectId)
        .ExecuteDeleteAsync();

        return Ok(project);
    }

    // [HttpPatch("{projectId}")]
    // public async Task<IActionResult> UpdateProject(Guid projectId)
    // {
    //     var project = await _db.Projects
    //     .Where(p => p.Id == projectId)
    //     .ExecuteUpdateAsync();

    //     return Ok(project);
    // }

    //get all projects a user is a member of
    [HttpGet("{userId}/memberof")]
    public  async Task<IActionResult> GetUserMembership(Guid userId)
    {   
        var memberships = await _db.ProjectMembers
        .Where(membership => membership.UserId == userId)
        .Include(membership => membership.Project)
        .ToListAsync();

        return Ok(memberships);
    }

    [HttpPost("{projectId}/members")]
    public async Task<IActionResult> NewMembership(Guid projectId, Guid userId)
    {
        var membership = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId
            //role = 
        };

        _db.ProjectMembers.Add(membership);
        await _db.SaveChangesAsync();
        return Ok(membership);
    }

    [HttpGet("{projectId}/members")]
     public async Task<IActionResult> GetMembers(Guid projectId)
    {   
        var members = await _db.ProjectMembers
        .Where(member => member.ProjectId == projectId)
        .Include(member => member.User)
        .ToListAsync();

        return Ok(members);
    }

    [HttpDelete("{projectId}/members/{userId}")]
    public async Task<IActionResult> LeaveProject(Guid projectId, Guid userId)
    {
        var member = await _db.ProjectMembers
        .Where(m => m.ProjectId == projectId && m.UserId == userId)
        .ExecuteDeleteAsync();

        return Ok(member);
    }

    // [HttpPatch("{projectId}/members/{userId}")]
    // public async Task<IActionResult> ChangeRole(Guid projectId, Guid userId)
    // {
    //     //need roles done to get this working
    //     return Ok(member);
    // }

}