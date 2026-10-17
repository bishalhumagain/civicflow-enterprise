using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using System.Text.Json.Serialization;
using PublicServiceCaseManagement.Api.Features.Cases;

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

var connectionString = builder.Configuration.GetConnectionString("CivicFlow")
                       ?? throw new InvalidOperationException(
                           "Connection string 'CivicFlow' is not configured.");

builder.Services.AddDbContext<CivicFlowDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<ICaseService, CaseService>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();