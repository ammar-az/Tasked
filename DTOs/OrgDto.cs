namespace Tasked.DTOs;

public record OrgDto
{
    public Guid Id {get; init;}
    required public string Name {get; init;}
}
