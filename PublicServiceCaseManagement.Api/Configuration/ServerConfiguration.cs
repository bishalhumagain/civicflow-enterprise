namespace PublicServiceCaseManagement.Api.Configuration;

/// <summary>
/// Centralized server configuration including ports and URLs.
/// All configurable server settings should be centralized here.
/// </summary>
public sealed class ServerConfiguration
{
    public const string SectionName = "Server";

    /// <summary>
    /// The host name or IP address the API should bind to.
    /// Defaults to "http://localhost" for development.
    /// </summary>
    public string ApiHost { get; set; } = "http://localhost";

    /// <summary>
    /// The port number for the API server.
    /// Defaults to 5000.
    /// Can be overridden via environment variable API_HOST_PORT.
    /// </summary>
    public int ApiPort { get; set; } = 5000;

    /// <summary>
    /// The base URL for the API (constructed from ApiHost and ApiPort).
    /// </summary>
    public string ApiBaseUrl => $"{ApiHost}:{ApiPort}";

    /// <summary>
    /// The base URL for the frontend application.
    /// Must match the Frontend:BaseUrl in appsettings.
    /// </summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>
    /// The port number for the frontend application.
    /// Defaults to 5173 (Vite default).
    /// </summary>
    public int FrontendPort { get; set; } = 5173;

    /// <summary>
    /// PostgreSQL database host.
    /// Defaults to localhost.
    /// </summary>
    public string DatabaseHost { get; set; } = "localhost";

    /// <summary>
    /// PostgreSQL database port.
    /// Defaults to 5432.
    /// Can be overridden via environment variable POSTGRES_HOST_PORT.
    /// </summary>
    public int DatabasePort { get; set; } = 5432;

    /// <summary>
    /// Environment (Development, Staging, Production).
    /// Defaults to Development.
    /// </summary>
    public string Environment { get; set; } = "Development";
}
