using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.Models;

namespace Tasked.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
     private readonly ApplicationDbContext _db;

    public UsersController(ApplicationDbContext db)
    {
        _db = db;
    }

    //register user
    [HttpPost]
    public async Task<IActionResult> Register(string username)
    {
        var user = new User
        {
            Username = username
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(user);
    }
    
    //get user by username

    //get users in org

    //get users working on project 

    //delete user account

    //put update account details

    //put update membership

}