using System;
namespace Luma;
public enum NativeStartAction { None, DismissNative, ShowLauncher, Stop }
public sealed class NativeStartHandoffPolicy
{
    public bool Pending { get; private set; }
    int attempts;
    bool observedVisible;
    public bool Begin(bool enabled, bool nativeVisible)
    {
        if (!nativeVisible) { observedVisible = false; return false; }
        if (!enabled) { observedVisible = false; return false; }
        if (Pending || observedVisible) return false;
        observedVisible = true;
        Pending = true; attempts = 0; return true;
    }
    public NativeStartAction Next(bool enabled, bool nativeVisible, bool foregroundIsStart, bool winHeld, int elapsed)
    {
        if (!Pending) return NativeStartAction.None;
        if (!enabled) { observedVisible = false; Cancel(); return NativeStartAction.Stop; }
        if (elapsed > 3000) { Cancel(); return NativeStartAction.Stop; }
        if (!nativeVisible) { observedVisible = false; Cancel(); return NativeStartAction.ShowLauncher; }
        if (winHeld || !foregroundIsStart) return NativeStartAction.None;
        if (attempts >= 2) return NativeStartAction.None;
        attempts++; return NativeStartAction.DismissNative;
    }
    public void Cancel() { Pending = false; attempts = 0; }
    public static bool IsStartHost(string process, bool shellConfirmed = false) => String.Equals(process, "StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase) ||
        (shellConfirmed && (String.Equals(process, "ShellHost", StringComparison.OrdinalIgnoreCase) || String.Equals(process, "ShellExperienceHost", StringComparison.OrdinalIgnoreCase)));
}
