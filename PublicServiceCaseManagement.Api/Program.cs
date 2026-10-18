using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using System.Text.Json.Serialization;
using PublicServiceCaseManagement.Api.Features.Cases;
using PublicServiceCaseManagement.Api.Features.Organizations;
using PublicServiceCaseManagement.Api.Features.Staff;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddScoped<ICaseService, CaseService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<ICurrentOrganizationContext, DevelopmentCurrentOrganizationContext>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
public partial class Program { }