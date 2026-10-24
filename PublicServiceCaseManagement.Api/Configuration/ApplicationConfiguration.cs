namespace PublicServiceCaseManagement.Api.Configuration;

/// <summary>
/// Product branding exposed by the public configuration endpoint. Only values
/// that are safe for anonymous clients belong here.
/// </summary>
public sealed class ApplicationConfiguration
{
    public const string SectionName = "Application";

    public string Name { get; set; } = "CivicFlow Enterprise";
}
