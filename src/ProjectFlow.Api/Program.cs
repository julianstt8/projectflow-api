using System.Text.Json.Serialization;
using Mediator;
using ProjectFlow.Api.Authentication;
using ProjectFlow.Api.ErrorHandling;
using ProjectFlow.Api.Observability;
using ProjectFlow.Api.OpenApi;
using ProjectFlow.Api.Organizations;
using ProjectFlow.Application;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Behaviors;
using ProjectFlow.Infrastructure;
using ProjectFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddProjectFlowLogging();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentOrganization, RouteCurrentOrganization>();

builder.Services.AddMediator((MediatorOptions options) =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies = [typeof(ProjectFlow.Application.AssemblyReference)];
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
});

builder.AddJwtAuthentication();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProjectFlowOpenApi();
builder.Services.AddProjectFlowProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCorrelationId();
app.UseProjectFlowRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

// Off by default outside Development; the public demo turns them on by configuration (ADR 0010).
if (app.Configuration.GetValue("ApiReference:Enabled", app.Environment.IsDevelopment()))
{
    app.MapProjectFlowApiReference();
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.ApplyMigrationsAsync();
}

if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
{
    await app.Services.SeedDevelopmentDataAsync();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

/// <summary>Entry point. Public and partial so the integration tests can host the API.</summary>
public partial class Program;
