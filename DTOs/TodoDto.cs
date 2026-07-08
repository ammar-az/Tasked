using Tasked.Enums;

namespace Tasked.DTOs;

public record TodoDto
{
    required public Guid Id {get; init;}
    required public Guid ProjectId {get; init;}
    required public string Title {get; init;} 
    public string? Description {get; init;}
    required public TodoStatus Status {get; init;}
    public Guid? Assigned {get; init;}
    required public DateTime CreatedAt {get; init;}
    required public int IssueNo {get; init;}
}