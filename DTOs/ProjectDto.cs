namespace Tasked.DTOs;

public record ProjectDto
{
    required public Guid Id {get; init;}
    required public Guid OwnerId {get; init;}
    required public string Name {get; init;} 
    public string? Description {get; init;}
    public Guid? OrgId {get; init;}
    public string? OrgName {get; init;}
    public bool IsVisible {get; init;}
}

public record ProjectRequest
{
    required public string Name {get; init;}
    public string? Description {get; init;}
    public Guid? OrgId {get; init;}
    public bool IsVisible {get; init;}
}