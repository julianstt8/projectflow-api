using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Application.Authentication.Login;
using ProjectFlow.Application.Authentication.Register;
using ProjectFlow.Application.Organizations;
using Scalar.AspNetCore;

namespace ProjectFlow.Api.OpenApi;

/// <summary>
/// The OpenAPI document (built into .NET 10, summaries from the XML comments of the controllers) and the
/// Scalar reference to try every endpoint from the browser. Development only.
/// </summary>
internal static class OpenApiSetup
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddProjectFlowOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "ProjectFlow API",
                    Version = "v1",
                    Description =
                        "Software project management API (mini-Jira): organizations, projects, sprints, epics, tasks, " +
                        "comments, labels, activity log and search, with JWT authentication and role-based access control. " +
                        "Log in with `POST /api/auth/login`, then use the access token as a Bearer token.",
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Access token from `POST /api/auth/login` (valid 15 minutes).",
                };

                return Task.CompletedTask;
            });

            // Every endpoint needs a token except the ones marked [AllowAnonymous]: document exactly that.
            options.AddOperationTransformer((operation, context, _) =>
            {
                var isAnonymous = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
                if (!isAnonymous)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
                    });
                }

                return Task.CompletedTask;
            });

            options.AddSchemaTransformer((schema, context, _) =>
            {
                if (Examples.TryGetValue(context.JsonTypeInfo.Type, out var example))
                {
                    schema.Examples = [JsonNode.Parse(example)!];
                }

                return Task.CompletedTask;
            });
        });

    public static WebApplication MapProjectFlowApiReference(this WebApplication app)
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options => options
                .WithTitle("ProjectFlow API")
                .AddPreferredSecuritySchemes(BearerScheme))
            .AllowAnonymous();
        app.MapGet("/", () => Results.Redirect("/scalar")).AllowAnonymous().ExcludeFromDescription();

        return app;
    }

    /// <summary>Realistic request bodies shown in the reference (and prefilled when trying an endpoint).</summary>
    private static readonly Dictionary<Type, string> Examples = new()
    {
        [typeof(RegisterUserCommand)] = """{ "email": "ana.admin@example.com", "password": "ProjectFlow-Dev-2026", "fullName": "Ana Admin" }""",
        [typeof(LoginCommand)] = """{ "email": "ana.admin@example.com", "password": "ProjectFlow-Dev-2026" }""",
        [typeof(CreateOrganizationCommand)] = """{ "name": "Acme Software", "slug": "acme-software" }""",
        [typeof(AddMemberRequest)] = """{ "email": "carla.dev@example.com", "role": "Member" }""",
        [typeof(CreateProjectRequest)] = """{ "key": "WEB", "name": "ProjectFlow Web", "description": "Customer-facing web application." }""",
        [typeof(AddProjectMemberRequest)] = """{ "email": "carla.dev@example.com", "role": "Developer" }""",
        [typeof(SprintRequest)] = """{ "name": "Sprint 3", "goal": "Password recovery", "startDate": "2026-10-12", "endDate": "2026-10-25" }""",
        [typeof(EpicRequest)] = """{ "name": "Authentication", "description": "Sign-up, login and password recovery." }""",
        [typeof(CreateTaskRequest)] = """{ "type": "Bug", "title": "Login fails with uppercase e-mail", "description": "Steps: log in with ANA@EXAMPLE.COM.", "priority": "Critical", "storyPoints": 2, "assigneeId": null, "sprintId": null, "epicId": null }""",
        [typeof(UpdateTaskRequest)] = """{ "type": "Bug", "title": "Login fails with uppercase e-mail", "description": null, "priority": "High", "storyPoints": 3 }""",
        [typeof(ChangeTaskStatusRequest)] = """{ "status": "InProgress" }""",
        [typeof(CommentRequest)] = """{ "body": "¡Corregido! La comparación de correos ya no distingue mayúsculas." }""",
        [typeof(LabelRequest)] = """{ "name": "backend", "color": "#1D76DB" }""",
    };
}
