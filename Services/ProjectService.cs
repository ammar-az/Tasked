namespace Tasked.Services;
using Tasked.Entities;
using Tasked.Data;
using Tasked.Enums;
using Microsoft.EntityFrameworkCore;

public class ProjectService(ApplicationDbContext db)
{
    private readonly ApplicationDbContext _db = db;

    public async Task<bool> CanView(Project project, Guid? userId)
    {
        //Actually returns true when:

        if (project.IsVisible) return true;

        if(userId == null) return false;

        if (project.OrgId != null){
            var orgMember = await _db.Users.AnyAsync(user =>
                user.Id == userId &&
                user.OrgId == project.OrgId);
            
            if(orgMember) return true;
        }

        return await _db.ProjectMembers.AnyAsync(m =>
            m.ProjectId == project.Id &&
            m.UserId == userId &&
            m.Role != MemberRole.Banned && 
            m.Role != MemberRole.Left);
    }

    // public async Task<bool> CanContribute(Project project, Guid userId)
    // {
    //     var member = await _db.ProjectMembers.FirstOrDefaultAsync(m =>
    //         m.ProjectId == project.Id &&
    //         m.UserId == userId);

    //     if (member == null) return false;

    //     if (member.Role == MemberRole.Admin || member.Role == MemberRole.Contributor || member.Role == MemberRole.Owner) return true;

    //     return false;
    // }

    public bool CanContribute(ProjectMember member)
    {
        if (member.Role == MemberRole.Admin || member.Role == MemberRole.Contributor || member.Role == MemberRole.Owner) return true;
        return false;
    }

    public async Task<bool> AdminPermissions(Project project, Guid userId)
    {
        return await _db.ProjectMembers.AnyAsync(m =>
            m.ProjectId == project.Id &&
            m.UserId == userId &&
            (m.Role == MemberRole.Admin || m.Role == MemberRole.Owner));
    }

    public bool OwnsProject(Project project, Guid userId)
    {
        return project.OwnerId == userId;
    }

    public async Task<bool> CanJoin(Project project, Guid userId)
    {
        //Actually returns true when:

        //Project has open join policy
        //OR
        //Invite system implemented ? later
        
        //check for banned users can occur alongside checking preexisting membership by retaining banned users and giving them a role of banned instead of deleting the entry 

        if (project.JoinPolicy == JoinPolicy.Open) return true;



        return false;
    }
}