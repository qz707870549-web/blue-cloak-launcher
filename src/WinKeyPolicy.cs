using System.Collections.Generic;

namespace Luma;

// Pure input policy: repeats, chords and injected input must never toggle Luma.
public sealed class WinKeyPolicy
{
    private readonly HashSet<int> held = new HashSet<int>();
    private bool candidate;
    public bool Process(int key, bool down, bool injected, bool enabled, bool modifierHeld)
    {
        if (injected) return false;
        bool win = key == 0x5b || key == 0x5c;
        if (down)
        {
            if (!held.Add(key)) return false;
            if (win) candidate = enabled && held.Count == 1 && !modifierHeld;
            else candidate = false;
            return false;
        }
        bool known = held.Remove(key);
        bool toggle = win && known && candidate && enabled && held.Count == 0 && !modifierHeld;
        if (win) candidate = false;
        return toggle;
    }
    public void Cancel() { candidate = false; }
    public void Reset() { held.Clear(); candidate = false; }
}
