namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Central definition of ASP.NET Core Identity role names and the
/// authorization policy names that map onto them.
/// </summary>
public static class AppRoles
{
    public const string Citizen = "citizen";
    public const string Staff = "staff";
    public const string SuperAdmin = "superadmin";

    public const string CitizenPolicy = "CitizenClient";
    public const string StaffPolicy = "StaffClient";
    public const string SuperAdminPolicy = "SuperAdmin";
}
