using Microsoft.EntityFrameworkCore;

namespace Tasked.Models;
[PrimaryKey(nameof(ProjectId),nameof(UserId))]
public class ProjectMember
{
    public Guid ProjectId {get; set;}
    public Project Project {get; set;} = null!;
    public Guid UserId {get; set;}
    public User User {get; set;} = null!;
    //public enum Role {get; set;} not nullable, roles not decided yet   
}
