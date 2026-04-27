namespace Irion.DuckDB.NET.Test.Infrastructure;

public sealed class InfrastructureSmokeTests
{
    [Fact]
    public void Docker_test_environment_registers_named_containers()
    {
        var environment = new DockerTestEnvironmentBuilder()
            .AddContainer("custom", "alpine:3.20", container => container.WithCommand("sh", "-c", "exit 0"))
            .Build();

        environment.Containers.Keys.Should().Contain("custom");
    }
}
