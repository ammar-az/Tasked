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

    //get all projects a user owns 
    [HttpGet("{userId}")]
    public  async Task<IActionResult> FetchUserProjects(Guid userId)
    {   
        var projects = await _db.Projects
        .Where(project => project.OwnerId == userId)
        .ToListAsync();

        return Ok(projects);
    }

    //get all projects a user is a member of

    //get all projects belonging to an org

    //get project by name

    //delete a project

    //update project details

}