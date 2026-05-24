using Microsoft.EntityFrameworkCore;
using Tasked.Enums;

namespace Tasked.Entities;

[Index(nameof(ProjectId), nameof(IssueNo), IsUnique = true)]
public class Todo
{
    public Guid Id {get; set;}
    public Guid ProjectId {get; set;}
    public Project Project {get; set;} = null!;
    public string Title {get; set;} = "";
    public string? Description {get; set;} 
    public TodoStatus Status {get; set;} 
    public User? Assigned {get; set;}
    public Guid? AssignedID {get; set;}
    public DateTime CreatedAt {get; set;}
    public int IssueNo {get; set;}
}
