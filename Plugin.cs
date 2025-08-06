using System.Diagnostics;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;

namespace OnlyTurbo;

public unsafe sealed class Plugin : IDalamudPlugin
{
    // ---

    private delegate bool IsInputIdPressedDelegate(nint inputData, uint id);
    [Signature(
        "E9 ?? ?? ?? ?? 83 7F 44 02",
        DetourName = nameof(IsInputIdPressedDetour)
    )]
    private Hook<IsInputIdPressedDelegate>? IsInputIdPressedHook { get; init; }

    // ---

    [Signature("E9 ?? ?? ?? ?? B9 4F 01 00 00")]
    private readonly delegate* unmanaged<nint, uint, bool> isInputIdDown;

    // ---

    private delegate void CheckHotbarBindingsDelegate(nint a1, byte a2);
    [Signature(
        "89 54 24 10 53 41 55 41 57",
        DetourName = nameof(CheckHotbarBindingsDetour)
    )]
    private Hook<CheckHotbarBindingsDelegate>? CheckHotbarBindingsHook { get; init; }

    // ---

    private readonly Stopwatch _delayStopwatch = new();
    private const int InitialDelayMs = 400;
    private const int RepeatDelayMs = 200;
    private bool _heldLastFrame = false;
    private bool _heldThisFrame = false;
    private long _delayMs = 0;

    private const uint FirstHotbarId = 57; // Hotbar 1, slot 1
    private const uint LastHotbarId = 188; // Pet hotbar, slot 12

    public Plugin(IDalamudPluginInterface pluginInterface, IFramework framework, IPluginLog log, IGameInteropProvider gameInterop)
    {
        DalamudService.Initialize(pluginInterface);

        gameInterop.InitializeFromAttributes(this);

        if (IsInputIdPressedHook == null)
            log.Error("IsInputIdPressed not found");

        if (isInputIdDown == null)
            log.Error("IsInputIdDown not found");

        if (CheckHotbarBindingsHook == null)
            log.Error("CheckHotbarBindings not found");

        CheckHotbarBindingsHook?.Enable();
    }

    public void Dispose()
    {
        IsInputIdPressedHook?.Dispose();
        CheckHotbarBindingsHook?.Dispose();
    }

    private bool IsInputIdPressedDetour(nint inputData, uint id)
    {
        if (isInputIdDown == null || id < FirstHotbarId || id > LastHotbarId)
            return IsInputIdPressedHook!.Original(inputData, id);

        bool held = isInputIdDown(inputData, id);

        if (held)
        {
            _heldThisFrame = true;
            if (_delayStopwatch.ElapsedMilliseconds >= _delayMs)
            {
                long delta = _delayMs - _delayStopwatch.ElapsedMilliseconds;
                if (_delayMs == 0)
                    _delayMs = InitialDelayMs + delta;
                else
                    _delayMs = RepeatDelayMs + delta;
                _delayStopwatch.Restart();
                return true;
            }
        }

        return IsInputIdPressedHook!.Original(inputData, id);
    }

    private void CheckHotbarBindingsDetour(nint a1, byte a2)
    {
        if (!_heldLastFrame && _delayStopwatch.IsRunning)
        {
            _delayMs = 0;
            _delayStopwatch.Reset();
        }
        IsInputIdPressedHook?.Enable();
        CheckHotbarBindingsHook?.Original(a1, a2);
        IsInputIdPressedHook?.Disable();
        _heldLastFrame = _heldThisFrame;
        _heldThisFrame = false;
    }
}
