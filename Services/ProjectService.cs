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
            m.Role != MemberRole.Banned);
    }

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
        if (project.JoinPolicy == JoinPolicy.Open || project.JoinPolicy == JoinPolicy.ViewOnly) return true;

        if (project.JoinPolicy == JoinPolicy.InviteOnly)
        {
            var invite = await _db.ProjectMembers
                .AsNoTracking()
                .Where(m => m.ProjectId == project.Id && m.UserId == userId && m.Role == MemberRole.Invited)
                .AnyAsync();

            return invite;
        }
        
        return false;
    }
}