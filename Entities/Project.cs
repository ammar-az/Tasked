using Microsoft.EntityFrameworkCore;
using Tasked.Enums;

namespace Tasked.Entities;

[Index(nameof(Slug), IsUnique = true)]
[Index(nameof(Name), IsUnique = true)]
public class Project
{
    public Guid Id {get; set;}
    public Guid OwnerId {get; set;}
    public User Owner {get; set;} = null!;
    public Guid? OrgId {get; set;}
    public Organization? Org {get; set;}
    public string Name {get; set;} = "";
    public string Slug {get; set;} = "";
    public string? Description {get; set;}
    public bool IsVisible {get; set;}
    public int IssueCount {get; set;}
    public JoinPolicy JoinPolicy {get; set;}
    public DateTime CreatedAt {get; set;}
    public ICollection<Todo> Todos {get; set;} = [];
    public ICollection<ProjectMember> Members {get;} = [];
}
