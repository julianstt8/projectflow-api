using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using ProjectFlow.Api.IntegrationTests.Infrastructure;

namespace ProjectFlow.Api.IntegrationTests.Authentication;

[Collection(ApiCollection.Name)]
public class SigningKeyConfigurationTests(ProjectFlowApiFactory api)
{
    [Fact]
    public void Outside_development_the_api_does_not_start_without_a_signing_key()
    {
        using var production = api.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var exception = Assert.ThrowsAny<Exception>(() => production.CreateClient());

        Assert.IsType<OptionsValidationException>(exception.GetBaseException());
    }

    [Fact]
    public void Outside_development_the_api_does_not_start_with_a_short_signing_key()
    {
        using var production = api.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("Jwt:SigningKey", "too-short"));

        var exception = Assert.ThrowsAny<Exception>(() => production.CreateClient());

        Assert.IsType<OptionsValidationException>(exception.GetBaseException());
    }
}
