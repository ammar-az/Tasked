using Tasked.Enums;

namespace Tasked.DTOs;

public record ProjectDto
{
    required public Guid Id {get; init;}
    required public Guid OwnerId {get; init;}
    required public string OwnerName {get; init;}
    required public string Name {get; init;} 
    required public string Slug {get; init;} 
    public string? Description {get; init;}
    public Guid? OrgId {get; init;}
    public string? OrgName {get; init;}
    required public bool IsVisible {get; init;}
    required public JoinPolicy JoinPolicy {get; init;}
    required public DateTime CreatedAt {get; init;}
}

public record ProjectRequest
{
    required public string Name {get; init;}
    public string? Description {get; init;}
    public bool Org {get; init;}
    public bool IsVisible {get; init;}
    public JoinPolicy JoinPolicy {get; init;}
}

public record ProjectUpdateRequest
{
    public string? Name {get; init;}
    public string? Description {get; init;}
    public bool? IsVisible {get; init;}
    public JoinPolicy? JoinPolicy {get; init;}
}
