namespace Irion.DuckDB.NET.Test.Infrastructure;

public abstract class DockerIntegrationFixture : IAsyncLifetime
{
    public DockerTestEnvironment Environment { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = new DockerTestEnvironmentBuilder();
        Configure(builder);
        Environment = builder.Build();

        await Environment.InitializeAsync();
        await InitializeServicesAsync();
    }

    public Task DisposeAsync()
    {
        return Environment is null ? Task.CompletedTask : Environment.DisposeAsync();
    }

    protected abstract void Configure(DockerTestEnvironmentBuilder builder);

    protected virtual Task InitializeServicesAsync()
    {
        return Task.CompletedTask;
    }
}
