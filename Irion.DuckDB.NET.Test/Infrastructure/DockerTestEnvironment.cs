namespace Irion.DuckDB.NET.Test.Infrastructure;

public sealed class DockerTestEnvironment : IAsyncLifetime
{
    private readonly Dictionary<string, IContainer> containers;
    private readonly List<string> startOrder = new();

    internal DockerTestEnvironment(IReadOnlyDictionary<string, Func<IContainer>> registrations)
    {
        containers = registrations.ToDictionary(pair => pair.Key, pair => pair.Value(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, IContainer> Containers => containers;

    public TContainer Get<TContainer>(string name)
        where TContainer : class, IContainer
    {
        if (!containers.TryGetValue(name, out var container))
        {
            throw new KeyNotFoundException($"Container '{name}' is not registered.");
        }

        if (container is not TContainer typedContainer)
        {
            throw new InvalidCastException($"Container '{name}' is '{container.GetType().Name}', not '{typeof(TContainer).Name}'.");
        }

        return typedContainer;
    }

    public Task InitializeAsync()
    {
        return StartAsync(CancellationToken.None);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (name, container) in containers)
        {
            await container.StartAsync(cancellationToken);
            startOrder.Add(name);
        }
    }

    public Task DisposeAsync()
    {
        return StopAsync(CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        foreach (var name in startOrder.AsEnumerable().Reverse())
        {
            await containers[name].DisposeAsync();
        }

        startOrder.Clear();
    }
}
