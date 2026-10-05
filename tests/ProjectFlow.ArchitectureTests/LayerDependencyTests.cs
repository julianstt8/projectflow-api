using System.Reflection;

namespace ProjectFlow.ArchitectureTests;

public class LayerDependencyTests
{
    private const string Domain = "ProjectFlow.Domain";
    private const string Application = "ProjectFlow.Application";
    private const string Infrastructure = "ProjectFlow.Infrastructure";
    private const string Api = "ProjectFlow.Api";

    public static TheoryData<string, string> ForbiddenDependencies => new()
    {
        { Domain, Application },
        { Domain, Infrastructure },
        { Domain, Api },
        { Application, Infrastructure },
        { Application, Api },
        { Infrastructure, Api },
    };

    [Theory]
    [MemberData(nameof(ForbiddenDependencies))]
    public void Layer_does_not_depend_on_outer_layer(string layer, string forbidden)
    {
        var references = Assembly.Load(layer)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.DoesNotContain(forbidden, references);
    }
}
