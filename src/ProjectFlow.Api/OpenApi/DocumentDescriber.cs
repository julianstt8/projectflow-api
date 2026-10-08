using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace ProjectFlow.Api.OpenApi;

/// <summary>Applies the texts of one language to the whole document: info, security scheme, tags and request schemas.</summary>
internal static class DocumentDescriber
{
    public static void Describe(OpenApiDocument document, string language)
    {
        document.Info = new OpenApiInfo { Title = "ProjectFlow API", Version = "v1", Description = ApiCatalog.Description.In(language) };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[OpenApiSetup.BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = ApiCatalog.BearerDescription.In(language),
        };

        document.Tags = new HashSet<OpenApiTag>(ApiCatalog.Tags.Select(tag => new OpenApiTag
        {
            Name = tag.Name.In(language),
            Description = tag.Description.In(language),
        }));

        document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        document.Extensions["x-tagGroups"] = new JsonNodeExtension(new JsonArray([.. ApiCatalog.TagGroups.Select(group => new JsonObject
        {
            ["name"] = group.Name.In(language),
            ["tags"] = new JsonArray([.. group.Controllers.Select(controller => (JsonNode)ApiCatalog.TagOf(controller).Name.In(language))]),
        })]));

        foreach (var (name, schema) in document.Components.Schemas ?? new Dictionary<string, IOpenApiSchema>())
        {
            DescribeSchema(name, schema, language);
        }
    }

    private static void DescribeSchema(string name, IOpenApiSchema schema, string language)
    {
        if (schema is not OpenApiSchema described)
        {
            return;
        }

        if (ApiCatalog.Fields.TryGetValue(name, out var text))
        {
            described.Description = text.In(language);
        }

        foreach (var (property, propertySchema) in described.Properties ?? new Dictionary<string, IOpenApiSchema>())
        {
            if (!ApiCatalog.Fields.TryGetValue($"{name}.{property}", out var propertyText))
            {
                continue;
            }

            switch (propertySchema)
            {
                case OpenApiSchema inline:
                    inline.Description = propertyText.In(language);
                    break;
                case OpenApiSchemaReference reference:
                    reference.Description = propertyText.In(language);
                    break;
            }
        }
    }
}
