using Microsoft.EntityFrameworkCore;

namespace Tasked.Models;

//[Index(nameof(Email), IsUnique = true)]
[Index(nameof(Username), IsUnique = true)]
public class User
{
    public Guid Id {get; set;}
    public string Username {get; set;} = "";
    public Guid? OrgId {get; set;} 
    public Organization? Org {get; set;}

    //public string PasswordHash {get; set;} = "";
    //public string Email {get; set;} = "";

    public ICollection<Project> OwnedProjects {get;} = [];
    //Might need Owned/Assigned Tasks later
}
