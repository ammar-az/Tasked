namespace Tasked.Models;
public class ProjectMember
{
    public Guid ProjectId {get; set;}
    public Guid UserId {get; set;}
    //public enum Role {get; set;} not nullable, roles not decided yet   
}
