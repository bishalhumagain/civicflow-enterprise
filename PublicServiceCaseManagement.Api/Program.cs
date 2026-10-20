
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PublicServiceCaseManagement.Api.Features.Cases;
using PublicServiceCaseManagement.Api.Features.Memberships;
using PublicServiceCaseManagement.Api.Features.Organizations;
using PublicServiceCaseManagement.Api.Features.Staff;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Files;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var isDevelopment = builder.Environment.IsDevelopment();

var authority = builder.Configuration["Authentication:Authority"]
    ?? (isDevelopment
        ? "http://localhost:8080/realms/civicflow"
        : throw new InvalidOperationException(
            "Authentication:Authority must be configured outside Development."));

var audience = builder.Configuration["Authentication:Audience"]
    ?? (isDevelopment
        ? "civicflow-api"
        : throw new InvalidOperationException(
            "Authentication:Audience must be configured outside Development."));

var staffClientId = builder.Configuration["Authentication:StaffClientId"]
    ?? (isDevelopment
        ? "civicflow-staff"
        : throw new InvalidOperationException(
            "Authentication:StaffClientId must be configured outside Development."));

var citizenClientId = builder.Configuration["Authentication:CitizenClientId"]
    ?? (isDevelopment
        ? "civicflow-citizen"
        : throw new InvalidOperationException(
            "Authentication:CitizenClientId must be configured outside Development."));

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = !isDevelopment;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authority,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StaffClient", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("azp", staffClientId);
    });

    options.AddPolicy("CitizenClient", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("azp", citizenClientId);
    });

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("azp", staffClientId)
        .Build();
});

builder.Services.AddDbContext<CivicFlowDbContext>((serviceProvider, options) =>
{
    var configuration =
        serviceProvider.GetRequiredService<IConfiguration>();

    var connectionString = configuration.GetConnectionString("CivicFlow")
                           ?? throw new InvalidOperationException(
                               "Connection string 'CivicFlow' is not configured.");

    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<ICaseService, CaseService>();
builder.Services.AddScoped<ICaseDocumentService, CaseDocumentService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IBusinessEndpointAccess, BusinessEndpointAccess>();
builder.Services.AddHttpContextAccessor();

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
builder.Services.AddScoped<IOrganizationMembershipService, OrganizationMembershipService>();
builder.Services.AddScoped<PublicServiceCaseManagement.Api.Features.Citizens.ICitizenProfileService, PublicServiceCaseManagement.Api.Features.Citizens.CitizenProfileService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OrganizationTransactionMiddleware>();

app.MapControllers();

app.Run();

public partial class Program { }
