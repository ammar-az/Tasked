using Tasked.Enums;

namespace Tasked.DTOs;

public class TodoDto
{
    public Guid Id {get; set;}
    public Guid ProjectId {get; set;}
    public string Title {get; set;} = "";
    public string? Description {get; set;}
    public TodoStatus Status {get; set;}
    public Guid? Assigned {get; set;}
    public DateTime CreatedAt {get; set;}
    public int IssueNo {get; set;}
}