namespace Tasked.Jwt;
using Tasked.Entities;
public class AuthService
{
    public bool CanView(Project project, Guid userId)
    {
        //Actually returns true when:

        //Project is public
        //OR
        //User is a member of the project
        //OR
        //User is a member of the project's organization

        return true;
    }

    public bool AdminPermissions(Project project, Guid userId)
    {
        //Actually returns true when:

        //User is the owner of the project
        //OR
        //User is an admin of the project's organization

        return true;
    }

    public bool OwnsProject(Project project, Guid userId)
    {
        return project.OwnerId == userId;
    }
}