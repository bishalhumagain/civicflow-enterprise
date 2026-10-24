
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Configuration;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Audit;
using PublicServiceCaseManagement.Api.Features.Auth;
using PublicServiceCaseManagement.Api.Features.BackgroundJobs;
using PublicServiceCaseManagement.Api.Features.Cases;
using PublicServiceCaseManagement.Api.Features.Citizens;
using PublicServiceCaseManagement.Api.Features.Dashboard;
using PublicServiceCaseManagement.Api.Features.Errors;
using PublicServiceCaseManagement.Api.Features.Invitations;
using PublicServiceCaseManagement.Api.Features.Memberships;
using PublicServiceCaseManagement.Api.Features.Notifications;
using PublicServiceCaseManagement.Api.Features.Organizations;
using PublicServiceCaseManagement.Api.Features.Platform;
using PublicServiceCaseManagement.Api.Features.Profiles;
using PublicServiceCaseManagement.Api.Features.Staff;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Errors;
using PublicServiceCaseManagement.Api.Infrastructure.Files;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", Environments.Development);
}

var builder = WebApplication.CreateBuilder(args);

// Bind centralized server configuration
builder.Services.Configure<ServerConfiguration>(
    builder.Configuration.GetSection(ServerConfiguration.SectionName));
builder.Services.Configure<ApplicationConfiguration>(
    builder.Configuration.GetSection(ApplicationConfiguration.SectionName));

var isDevelopment = builder.Environment.IsDevelopment();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<CivicFlowDbContext>((serviceProvider, options) =>
{
    var configuration =
        serviceProvider.GetRequiredService<IConfiguration>();

    var connectionString = configuration.GetConnectionString("CivicFlow")
                           ?? throw new InvalidOperationException(
                               "Connection string 'CivicFlow' is not configured.");

    options.UseNpgsql(connectionString);
});

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 6;

        options.User.RequireUniqueEmail = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<CivicFlowDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(options =>
    {
        // A policy scheme picks the real handler per request: a bearer
        // personal access token goes to the token handler, everything else
        // uses the Identity application cookie. This keeps authorization
        // policies identical for browser and API clients.
        options.DefaultScheme = "CivicFlowSmart";
        options.DefaultAuthenticateScheme = "CivicFlowSmart";
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        options.DefaultForbidScheme = IdentityConstants.ApplicationScheme;
    })
    .AddIdentityCookies();

builder.Services
    .AddAuthentication()
    .AddScheme<
        AuthenticationSchemeOptions,
        PersonalAccessTokenAuthenticationHandler>(
        PersonalAccessTokenAuthenticationHandler.SchemeName,
        _ => { })
    .AddPolicyScheme("CivicFlowSmart", "CivicFlowSmart", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();

            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
                authorization.Contains(
                    PersonalAccessToken.TokenPrefix + "_",
                    StringComparison.Ordinal))
            {
                return PersonalAccessTokenAuthenticationHandler.SchemeName;
            }

            return IdentityConstants.ApplicationScheme;
        };
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    // The __Host- prefix is only honoured over HTTPS. Outside Development we
    // always require HTTPS, so the hardened prefix is used there; in
    // Development the cookie falls back to a plain name so it works over the
    // Vite proxy and in integration tests.
    options.Cookie.Name = isDevelopment
        ? "civicflow.auth"
        : "__Host-civicflow";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = isDevelopment
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);

    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// Revalidate the Identity security stamp on every request so an issued session
// stops working the moment the stamp changes — password change/reset, account
// deactivation, or a platform-role change — instead of lingering for the
// default 30-minute validation window.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppRoles.StaffPolicy, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AppRoles.Staff, AppRoles.SuperAdmin);
    });

    options.AddPolicy(AppRoles.CitizenPolicy, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AppRoles.Citizen);
    });

    options.AddPolicy(AppRoles.SuperAdminPolicy, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AppRoles.SuperAdmin);
    });

    options.AddPolicy(AppRoles.ProfilePolicy, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AppRoles.Citizen, AppRoles.Staff);
    });

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole(AppRoles.Staff, AppRoles.SuperAdmin)
        .Build();
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = isDevelopment
        ? "civicflow.csrf"
        : "__Host-civicflow-csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = isDevelopment
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("password", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("job-admin", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddScoped<ICaseService, CaseService>();
builder.Services.AddScoped<INotificationOutbox, NotificationOutbox>();
builder.Services.AddScoped<BackgroundJobProcessor>();
if (builder.Configuration.GetValue("BackgroundJobs:Enabled", true))
{
    builder.Services.AddHostedService<BackgroundJobWorker>();
}
builder.Services.AddScoped<ICaseDocumentService, CaseDocumentService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IBusinessEndpointAccess, BusinessEndpointAccess>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<
    IAuthenticatedOrganizationResolver,
    AuthenticatedOrganizationResolver>();

builder.Services.AddScoped<
    IOrganizationTransactionContext,
    OrganizationTransactionContext>();

builder.Services.AddScoped<
    ICurrentOrganizationContext,
    AuthenticatedCurrentOrganizationContext>();

builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IOrganizationTypeService, OrganizationTypeService>();
builder.Services.AddScoped<IOrganizationMembershipService, OrganizationMembershipService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<ICitizenProfileService, CitizenProfileService>();
builder.Services.AddScoped<IProfileService, ProfileService>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPersonalAccessTokenService, PersonalAccessTokenService>();
if (isDevelopment)
{
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender>(
        new SmtpEmailSender(builder.Configuration));
}

// The audit trail is scoped because it writes through the request scoped
// database context and resolves the actor from the current HTTP context.
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<IAuditTrailWriter, AuditTrailWriter>();
builder.Services.AddScoped<ICurrentActorResolver, CurrentActorResolver>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddScoped<IPlatformDirectoryService, PlatformDirectoryService>();

// Error logging
builder.Services.AddScoped<IErrorLogger, ErrorLogger>();

// Dashboard
builder.Services.AddScoped<IDashboardService, DashboardService>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

// Development-only trust rule for the Vite dev server. Vite hops to the next
// free port (5173, 5174, 5175, ...) whenever the preferred one is busy, so a
// fixed origin allowlist breaks login the moment the port changes. Cookies are
// scoped to localhost (shared across every port), which is exactly why the
// port must not be pinned here. Only first-party loopback http origins are
// accepted — never a public host, a wildcard, or arbitrary origins with
// credentials. This predicate is never consulted outside Development.
static bool IsTrustedLoopbackOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
        uri.Scheme != Uri.UriSchemeHttp)
    {
        return false;
    }

    if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return IPAddress.TryParse(uri.Host, out var address) &&
           IPAddress.IsLoopback(address);
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (isDevelopment)
        {
            policy
                .SetIsOriginAllowed(origin =>
                    IsTrustedLoopbackOrigin(origin) ||
                    Array.Exists(
                        allowedOrigins,
                        allowed => string.Equals(
                            allowed,
                            origin,
                            StringComparison.OrdinalIgnoreCase)))
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        // Non-development with no configured origins: no policy is registered,
        // so cross-origin requests are refused. The SPA is served same-origin
        // behind a reverse proxy in production, which needs no CORS.
    });
});

var app = builder.Build();

await IdentityDataSeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseRateLimiter();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SessionLifetimeMiddleware>();
app.UseMiddleware<AntiforgeryMiddleware>();
app.UseMiddleware<OrganizationTransactionMiddleware>();

app.MapControllers();

app.Run();

public partial class Program { }