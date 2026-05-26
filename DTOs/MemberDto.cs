using Tasked.Enums;

namespace Tasked.DTOs;

public class MemberDto
{
    public Guid UserId {get; set;}
    public string Username {get; set;} = "";
    public Guid ProjectId {get; set;}
    public string ProjectName {get; set;} = "";
    public MemberRole Role {get; set;}
    
    public Guid? OrgId {get; set;}
    public string? OrgName {get; set;}
}
