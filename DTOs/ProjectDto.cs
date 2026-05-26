namespace Tasked.DTOs;

public class ProjectDto
{
    public Guid Id {get; set;}
    public Guid OwnerId {get; set;}
    public string Name {get; set;} = "";
    public string? Description {get; set;}
    public Guid? OrgId {get; set;}
    public string? OrgName {get; set;}
}
