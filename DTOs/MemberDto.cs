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
