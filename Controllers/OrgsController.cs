using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.Models;

namespace Tasked.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrgsController : ControllerBase
{
     private readonly ApplicationDbContext _db;

    public OrgsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("{orgId}/projects")]
    public async Task<IActionResult> GetOrgProjects(Guid orgId)
    {
        var projects = await _db.Projects
        .Where(project => project.OrgId == orgId)
        .ToListAsync();

        return Ok(projects);
    }

    [HttpGet("{orgId}/users")]
    public async Task<IActionResult> GetOrgUsers(Guid orgId)
    {
        var users = await _db.Users
        .Where(user => user.OrgId == orgId)
        .ToListAsync();

        return Ok(users);
    }

    //post new org

    //get org by name

    //delete org 


}