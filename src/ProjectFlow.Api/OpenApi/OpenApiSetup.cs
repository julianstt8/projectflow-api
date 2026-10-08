using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace ProjectFlow.Api.OpenApi;

/// <summary>
/// The OpenAPI documents (built into .NET 10), one per language, and the Scalar reference to try every endpoint
/// from the browser, with a language selector. The texts come from <see cref="ApiCatalog"/>. Development only.
/// </summary>
internal static class OpenApiSetup
{
    public const string BearerScheme = "Bearer";

    private static readonly JsonSerializerOptions ExampleJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static IServiceCollection AddProjectFlowOpenApi(this IServiceCollection services)
    {
        foreach (var (language, _) in ApiLanguages.All)
        {
            services.AddOpenApi(language, options =>
            {
                options.AddSchemaTransformer(AddExample);
                options.AddOperationTransformer((operation, context, _) =>
                {
                    OperationDescriber.Describe(operation, context, language);
                    return Task.CompletedTask;
                });
                options.AddDocumentTransformer((document, _, _) =>
                {
                    DocumentDescriber.Describe(document, language);
                    return Task.CompletedTask;
                });
            });
        }

        return services;
    }

    public static WebApplication MapProjectFlowApiReference(this WebApplication app)
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options =>
            {
                options.WithTitle("ProjectFlow API").AddPreferredSecuritySchemes(BearerScheme);
                foreach (var (language, title) in ApiLanguages.All)
                {
                    options.AddDocument(language, title, isDefault: language == ApiLanguages.English);
                }
            })
            .AllowAnonymous();
        app.MapGet("/", () => Results.Redirect("/scalar")).AllowAnonymous().ExcludeFromDescription();

        return app;
    }

    private static Task AddExample(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonPropertyInfo is null && ApiCatalog.Examples.TryGetValue(context.JsonTypeInfo.Type, out var example))
        {
            schema.Examples = [JsonSerializer.SerializeToNode(example, context.JsonTypeInfo.Type, ExampleJson)!];
        }

        return Task.CompletedTask;
    }
}
