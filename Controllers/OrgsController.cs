using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.Entities;

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

    [HttpPost]
    public async Task<IActionResult> RegisterOrg(string name)
    {
        var org = new Organization
        {
            Name = name
        };

        _db.Organizations.Add(org);
        await _db.SaveChangesAsync();
        return Ok(org);
    }

    [HttpGet("{orgId}")]
    public async Task<IActionResult> GetOrgById(Guid orgId)
    {
        var org = await _db.Organizations
        .FindAsync(orgId);

        return Ok(org);
    }

    [HttpDelete("{orgId}")]
    public async Task<IActionResult> DeleteOrg(Guid orgId)
    {
        var org = await _db.Organizations
        .Where(o => o.Id == orgId)
        .ExecuteDeleteAsync();

        return Ok(org);
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

    //add/remove users from org 

}
