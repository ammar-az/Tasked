using Tasked.Enums;

namespace Tasked.DTOs;

public record TodoDto
{
    required public Guid Id {get; init;}
    required public Guid ProjectId {get; init;}
    required public string ProjectName {get; init;}
    required public string ProjectSlug {get; init;}
    required public string Title {get; init;} 
    public string? Description {get; init;}
    required public TodoStatus Status {get; init;}
    public Guid? Assigned {get; init;}
    public string? AssignedName {get; init;}
    public Guid? CreatedBy {get; init;}
    public string? CreatedByName {get; init;}
    required public DateTime CreatedAt {get; init;}
    required public int IssueNo {get; init;}
}

public record TodoRequest
{
    required public string Title {get; init;}
    public string? Description {get; init;}
    required public TodoStatus Status {get; init;} = TodoStatus.Backlog;
    required public bool SelfAssign  {get; init;} = false;
}

public record TodoUpdateRequest
{
    public string? Title {get; init;}
    public string? Description {get; init;}
    public TodoStatus? Status {get; init;}
    public Guid? Assigned {get; init;}
    public bool Unassign {get; init;} = false;
}

public record GetManyTodosRequest
{
    public string? Search {get; init;}
    public TodoStatus? Status {get; init;}

    public Guid? Assigned {get; init;}
    
    required public TodoSort SortBy {get; init;} = TodoSort.IssueNo;
    public bool Descending {get; init;}

    public int Page {get; init;} = 1;
    public int PageSize {get; init;} = 20;
}
