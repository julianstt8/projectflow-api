using System.Net;
using ProjectFlow.Api.IntegrationTests.Infrastructure;

namespace ProjectFlow.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HealthEndpointTests(ProjectFlowApiFactory api)
{
    [Fact]
    public async Task Get_health_returns_healthy()
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
