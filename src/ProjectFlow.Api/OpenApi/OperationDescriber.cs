using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using ProjectFlow.Api.ErrorHandling;
using ProjectFlow.Domain.Common;

namespace ProjectFlow.Api.OpenApi;

/// <summary>
/// Applies the texts of one language to an operation: title, description with who can call it, parameters, and every
/// response with its error codes and an example.
/// </summary>
internal static class OperationDescriber
{
    private const string TraceIdExample = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00";

    public static void Describe(OpenApiOperation operation, OpenApiOperationTransformerContext context, string language)
    {
        var action = (ControllerActionDescriptor)context.Description.ActionDescriptor;
        var access = EndpointAccess.For(action.EndpointMetadata);

        if (access.Errors.Contains(EndpointAccess.NotAuthenticated))
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(OpenApiSetup.BearerScheme, context.Document)] = [],
            });
        }

        operation.Tags = new HashSet<OpenApiTagReference> { new(ApiCatalog.TagOf(action.ControllerName).Name.In(language), context.Document) };

        var key = $"{action.ControllerName}.{action.ActionName}";
        var text = ApiCatalog.Operations.GetValueOrDefault(key);
        if (text is not null)
        {
            operation.Summary = text.Title.In(language);
            operation.Description =
                $"{text.Description.In(language)}\n\n**{ApiCatalog.WhoCanCallLabel.In(language)}:** {access.WhoCanCall.In(language)}";
        }

        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            // Query binding ignores case: show the names in camelCase, like the JSON fields.
            if (parameter.In == ParameterLocation.Query)
            {
                parameter.Name = JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!);
            }

            if (ApiCatalog.Parameters.TryGetValue($"{parameter.In}:{parameter.Name}", out var parameterText))
            {
                parameter.Description = parameterText.In(language);
            }
        }

        DescribeResponses(operation, context, key, access, text?.Errors ?? [], language);
    }

    private static void DescribeResponses(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        string key,
        EndpointAccess access,
        IReadOnlyList<Error> useCaseErrors,
        string language)
    {
        operation.Responses ??= [];

        // Validation.Failed comes from the request validators: the action declares it with a ValidationProblemDetails 400.
        var validated = context.Description.SupportedResponseTypes
            .Any(type => type.StatusCode == StatusCodes.Status400BadRequest && type.Type == typeof(ValidationProblemDetails));
        IEnumerable<Error> errors = validated ? [EndpointAccess.ValidationFailed, .. useCaseErrors] : useCaseErrors;

        foreach (var statusErrors in errors
            .Concat(access.Errors)
            .DistinctBy(error => error.Code)
            .GroupBy(error => error.Type.ToStatusCode().ToString(CultureInfo.InvariantCulture)))
        {
            if (!operation.Responses.TryGetValue(statusErrors.Key, out var response))
            {
                // Authorization errors happen before the action runs, so the action does not declare them.
                if (!statusErrors.All(access.Errors.Contains))
                {
                    throw new InvalidOperationException(
                        $"{key} documents {string.Join(", ", statusErrors.Select(error => error.Code))} but does not declare " +
                        $"[ProducesResponseType] for status {statusErrors.Key}.");
                }

                response = new OpenApiResponse();
                operation.Responses[statusErrors.Key] = response;
            }

            DescribeErrorResponse((OpenApiResponse)response, [.. statusErrors], context.Document, language);
        }

        // Authorization errors were added last: show every response in status order.
        var ordered = operation.Responses.OrderBy(response => response.Key, StringComparer.Ordinal).ToList();
        operation.Responses.Clear();
        foreach (var (status, response) in ordered)
        {
            operation.Responses[status] = response;
        }

        foreach (var (status, response) in operation.Responses)
        {
            if (response is OpenApiResponse described
                && int.TryParse(status, CultureInfo.InvariantCulture, out var code)
                && code < StatusCodes.Status300MultipleChoices
                && ApiCatalog.Statuses.TryGetValue(code, out var success))
            {
                described.Description = success.In(language);

                // The API answers JSON only; the formatter list (text/plain, text/json) is noise for readers.
                if (described.Content?.TryGetValue("application/json", out var json) == true)
                {
                    described.Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = json };
                }
            }
        }
    }

    private static void DescribeErrorResponse(OpenApiResponse response, IReadOnlyList<Error> errors, OpenApiDocument? document, string language)
    {
        var status = errors[0].Type.ToStatusCode();
        var codes = errors.Select(error => $"- `{error.Code}`: {ApiCatalog.ErrorTexts.GetValueOrDefault(error.Code).In(language)}");
        response.Description =
            $"{ApiCatalog.Statuses[status].In(language)} {ApiCatalog.PossibleCodes.In(language)}\n{string.Join('\n', codes)}";

        // Errors are always application/problem+json; keep the declared schema (ValidationProblemDetails for 400).
        var schema = response.Content?.Values.FirstOrDefault()?.Schema ?? new OpenApiSchemaReference("ProblemDetails", document);
        response.Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/problem+json"] = new() { Schema = schema, Example = ProblemExample(errors[0], status) },
        };
    }

    private static JsonObject ProblemExample(Error error, int status)
    {
        var problem = new JsonObject
        {
            ["type"] = ProblemTypes[status],
            ["title"] = error.Description,
            ["status"] = status,
        };

        if (error == EndpointAccess.ValidationFailed)
        {
            problem["errors"] = new JsonObject { ["Title"] = new JsonArray("'Title' must not be empty.") };
        }

        problem["code"] = error.Code;
        problem["traceId"] = TraceIdExample;
        return problem;
    }

    private static readonly Dictionary<int, string> ProblemTypes = new()
    {
        [StatusCodes.Status400BadRequest] = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        [StatusCodes.Status401Unauthorized] = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        [StatusCodes.Status403Forbidden] = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        [StatusCodes.Status404NotFound] = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        [StatusCodes.Status409Conflict] = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        [StatusCodes.Status422UnprocessableEntity] = "https://tools.ietf.org/html/rfc9110#section-15.5.21",
    };
}
