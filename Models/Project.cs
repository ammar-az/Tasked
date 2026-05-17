namespace Tasked.Models;
public class Project
{
    public Guid Id {get; set;}
    public Guid OwnerId {get; set;}
    public User Owner {get; set;} = null!;
    public Guid? OrgId {get; set;}
    public Organization? Org {get; set;}
    public string Name {get; set;} = "";
    public string? Description {get; set;}
    public ICollection<Todo> Todos {get; set;} = [];
}
