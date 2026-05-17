namespace Tasked.Models;
public class Project
{
    public Guid Id {get; set;}
    public Guid OwnerId {get; set;}
    public Guid? OrganizationId {get; set;}
    public string Name {get; set;} = "";
    public string? Description {get; set;}
}
