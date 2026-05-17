namespace Tasked.Models;

public class Todo
{
    public Guid Id {get; set;}
    public Guid ProjectId {get; set;}
    public Project Project {get; set;} = null!;

    public string Title {get; set;} = "";
    public string? Description {get; set;} 
    public bool IsComplete {get; set;}

    //public enum Status {get; set;} replaces IsComplete
    //public DateTime CreatedAt {get; set;}
    //public Guid PosterId {get; set;} 
}

