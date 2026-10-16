using Godot;

namespace DogeballVR.Xr;

/// <summary>Active OpenXR (Quest, plus tard PICO) et branche le viewport dessus.</summary>
public static class XrBootstrap
{
    public static bool Start(Viewport viewport)
    {
        var xr = XRServer.FindInterface("OpenXR");
        if (xr == null || !xr.IsInitialized() && !xr.Initialize())
        {
            GD.PrintErr("OpenXR indisponible : lancement en mode écran (test PC).");
            return false;
        }
        viewport.UseXR = true;
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled); // le casque cadence le rendu
        return true;
    }
}
