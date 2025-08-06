using Dalamud.Plugin;
namespace OnlyTurbo;

public sealed class Plugin : IDalamudPlugin
{
    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        DalamudService.Initialize(pluginInterface);
    }

    public void Dispose()
    {
    }
}
