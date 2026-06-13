using Microsoft.EntityFrameworkCore;

namespace Tasked.Entities;

[Index(nameof(Email), IsUnique = true)]
[Index(nameof(Username), IsUnique = true)]
public class User
{
    public Guid Id {get; set;}
    public string Username {get; set;} = "";
    public Guid? OrgId {get; set;} 
    public Organization? Org {get; set;}
    public string Email {get; set;} = "";
    public string Password {get; set;} = "";
    public ICollection<Project> OwnedProjects {get;} = [];
    public ICollection<Todo> AssignedTodos {get;} = [];
}
