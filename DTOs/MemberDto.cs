using Tasked.Enums;

namespace Tasked.DTOs;

public record MemberDto
{
    required public Guid UserId {get; init;}
    required public string Username {get; init;}
    required public Guid ProjectId {get; init;}
    required public string ProjectName {get; init;}
    required public MemberRole Role {get; init;}
    required public DateTime JoinTime {get; init;}
    public Guid? OrgId {get; init;}    
    public string? OrgName {get; init;}
}

public record MemberOverviewDto
{
    required public Guid ProjectId {get; init;}
    required public string ProjectName {get; init;}
    required public string ProjectSlug {get; init;}
    public string? ProjectDesc {get; init;}
    required public MemberRole Role {get; init;}
    public Guid? OrgId {get; init;}
    public string? OrgName {get; init;}
}

public record MemberOverviewRequest
{
    public string? Search {get; init;}
    public MemberRole? Role {get; init;}
    public bool Owner {get; init;}
    public string SortBy {get; init;} = "Name";
    public bool Descending {get; init;}

    public int Page {get; init;} = 1;
    public int PageSize {get; init;} = 20;
}

public record MemberRoleChangeRequest
{
    required public Guid User {get; init;}
    required public MemberRole Role {get; init;}
}
