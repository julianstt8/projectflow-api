using Mediator;
using ProjectFlow.Api.Organizations;
using ProjectFlow.Application;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Behaviors;
using ProjectFlow.Infrastructure;
using ProjectFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

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

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
