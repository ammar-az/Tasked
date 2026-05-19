using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasked.Data;
using Tasked.Models;

namespace Tasked.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganizationsController : ControllerBase
{
     private readonly ApplicationDbContext _db;

    public OrganizationsController(ApplicationDbContext db)
    {
        _db = db;
    }

    //post new org

    //get org by name

    //delete org 


}