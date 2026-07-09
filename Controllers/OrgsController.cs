using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.DTOs;
using Tasked.Entities;
using Tasked.Services;

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

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

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
                OwnerName = p.Owner.Username,
                Name = p.Name,
                Description = p.Description,
                OrgId = p.OrgId,
                OrgName = p.Org == null ? null : p.Org.Name,
                CreatedAt = p.CreatedAt,
                IsVisible = p.IsVisible,
                JoinPolicy = p.JoinPolicy
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
    [HttpPatch("{orgId}/add-user")]
    [Authorize]
    public async Task<IActionResult> AddUser(Guid orgId)
    {
        var userId = User.GetUserId();

        var user = await _db.Users
        .Where(u => u.Id == userId)
        .SingleOrDefaultAsync();

        if(user == null)
        {
            return NotFound("User not found");
        }

        if(user.OrgId != null)
        {
            return Conflict("Must leave current organization before joining another");
        }

        var org = await _db.Organizations
        .Where(o => o.Id == orgId) 
        .SingleOrDefaultAsync();

        if(org == null)
        {
            return NotFound("Organization not found");
        }   
        user.OrgId = orgId;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(DbUpdateException e)
        {
            return Conflict(e.InnerException?.Message);
        }

        return Ok();
    }
    
    [HttpPatch("{orgId}/remove-user")]
    [Authorize]
    public async Task<IActionResult> RemoveUser(Guid orgId)
    {
        var userId = User.GetUserId();

        var user = await _db.Users
        .Where(u => u.Id == userId)
        .SingleOrDefaultAsync();

        if(user == null)
        {
            return NotFound("User not found");
        }

        if(user.OrgId != orgId)
        {
            return Conflict("User does not belong to the specified organization");
        }

        user.OrgId = null;

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
}
