namespace Tasked.DTOs;

public record OrgDto
{
    public Guid Id {get; init;}
    required public string Name {get; init;}
}

public record OrgsRequest
{
    public string? Search {get; init;}
    public bool Descending {get; init;}
    public int Page {get; init;} = 1;
    public int PageSize {get; init;} = 20;
}
