namespace ProjectFlow.Api.OpenApi;

/// <summary>A text of the API reference in English and Spanish.</summary>
internal readonly record struct Localized(string En, string Es)
{
    public string In(string language) => language == ApiLanguages.Spanish ? Es : En;
}

/// <summary>The API reference is published once per language: <c>/openapi/en.json</c> and <c>/openapi/es.json</c>.</summary>
internal static class ApiLanguages
{
    public const string English = "en";
    public const string Spanish = "es";

    public static readonly IReadOnlyList<(string Name, string Title)> All = [(English, "English"), (Spanish, "Español")];
}
