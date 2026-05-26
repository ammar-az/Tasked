using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.DTOs;
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

        var dto = new OrgDto()
        {
            Id = org.Id,
            Name = org.Name
        };

        return CreatedAtAction(
            nameof(GetOrgById), 
            new { orgId = org.Id }, 
            dto
        );
    }

    [HttpGet]
    public async Task<IActionResult> GetOrgs()
    {
        var orgs = await _db.Organizations
        .AsNoTracking()
        .Select(o => 
            new OrgDto
            {
                Id = o.Id,
                Name = o.Name
            }).ToListAsync();

        return Ok(orgs);
    }

    [HttpGet("{orgId}")]
    public async Task<IActionResult> GetOrgById(Guid orgId)
    {
        var org = await _db.Organizations
        .AsNoTracking()
        .Where(o => o.Id == orgId)
        .Select(o => 
            new OrgDto
            {
                Id = o.Id,
                Name = o.Name
            }).SingleOrDefaultAsync();

        if(org == null)
        {
            return NotFound();
        }

        return Ok(org);
    }

    [HttpDelete("{orgId}")]
    public async Task<IActionResult> DeleteOrg(Guid orgId)
    {
        var org = await _db.Organizations
        .Where(o => o.Id == orgId)
        .Select(o => 
            new 
            {
                self = o,
                active = o.Projects.Any()
            }).SingleOrDefaultAsync();

        if(org == null)
        {
            return NotFound();
        }

        if(org.active)
        {
            return Conflict("Cannot delete an organization with active projects. Delete projects or have owners remove them from org umbrella first.");
        }

        _db.Organizations.Remove(org.self);
        await _db.SaveChangesAsync();

        return Ok(org);
    }

    [HttpGet("{orgId}/projects")]
    public async Task<IActionResult> GetOrgProjects(Guid orgId)
    {
        var projects = await _db.Projects
        .AsNoTracking()
        .Where(p => p.OrgId == orgId)
        .Select(p => 
            new ProjectDto
            {
                Id = p.Id,
                OwnerId = p.OwnerId,
                Name = p.Name,
                Description = p.Description,
                OrgId = p.OrgId,
                OrgName = p.Org == null ? null : p.Org.Name
            }).ToListAsync();

        return Ok(projects);
    }

    [HttpGet("{orgId}/users")]
    public async Task<IActionResult> GetOrgUsers(Guid orgId)
    {
        var users = await _db.Users
        .AsNoTracking()
        .Where(u => u.OrgId == orgId)
        .Select(u => 
            new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email
            }).ToListAsync();

        return Ok(users);
    }

    //add/remove users from org 

}
