using System.Text.Json.Serialization;
using Mediator;
using ProjectFlow.Api.Authentication;
using ProjectFlow.Api.ErrorHandling;
using ProjectFlow.Api.Observability;
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
builder.Services.AddOpenApi();
builder.Services.AddProjectFlowProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCorrelationId();
app.UseProjectFlowRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();

    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await app.Services.ApplyMigrationsAsync();
    }

    if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
    {
        await app.Services.SeedDevelopmentDataAsync();
    }
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
