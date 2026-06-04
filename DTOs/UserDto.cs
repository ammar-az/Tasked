namespace Tasked.DTOs;

public class UserDto
{
    public Guid Id {get; set;}
    public string Username {get; set;} = "";
    public Guid? OrgId {get; set;} 
    public string? OrgName {get; set;} 
    public string Email {get; set;} = "";
}
