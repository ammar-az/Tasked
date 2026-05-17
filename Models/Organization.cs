namespace Tasked.Models;
public class Organization
{
    public Guid Id {get; set;}
    public string Name {get; set;} = "";
    public ICollection<User> Users = [];
    public ICollection<Project> Projects = [];
}
