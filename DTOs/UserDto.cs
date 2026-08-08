namespace Tasked.DTOs;

public record UserDto
{
    required public Guid Id {get; init;}
    required public string Username {get; init;} 
    public Guid? OrgId {get; init;}    
    public string? OrgName {get; init;}
}

public record UserUpdateRequest
{
    public string? Username {get; init;}
}
