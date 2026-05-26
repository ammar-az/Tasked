using Microsoft.EntityFrameworkCore;

namespace Tasked.Entities;

[Index(nameof(Name), IsUnique = true)]
public class Organization
{
    public Guid Id {get; set;}
    public string Name {get; set;} = "";
    public ICollection<User> Users {get;} = [];
    public ICollection<Project> Projects {get;} = [];
}
