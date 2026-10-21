using System.Collections.Generic;
namespace Luma;
public enum FullscreenWinAction { Pass, Suppress, Toggle, ReplayChord }
// Standalone fullscreen Win presses never depend on synthetic input.
public sealed class FullscreenWinKeyPolicy
{
    readonly HashSet<int> wins = new HashSet<int>();
    bool cancelled, replayed;
    public bool IsHandling => wins.Count != 0;
    public int PendingWin { get; private set; }
    public FullscreenWinAction Process(int key, bool down, bool injected, bool enabled, bool fullscreen, bool modifier)
    {
        if (injected) return FullscreenWinAction.Pass;
        bool win = key == 0x5b || key == 0x5c;
        if (!IsHandling)
        {
            if (!win || !down || !enabled || !fullscreen || modifier) return FullscreenWinAction.Pass;
            PendingWin = key; wins.Add(key); cancelled = replayed = false;
            return FullscreenWinAction.Suppress;
        }
        if (win)
        {
            if (down)
            {
                if (wins.Add(key)) cancelled = true;
                return replayed ? FullscreenWinAction.Pass : FullscreenWinAction.Suppress;
            }
            if (!wins.Remove(key)) return FullscreenWinAction.Pass;
            bool toggle = wins.Count == 0 && !cancelled && !replayed && enabled;
            var action = replayed ? FullscreenWinAction.Pass : toggle ? FullscreenWinAction.Toggle : FullscreenWinAction.Suppress;
            if (wins.Count == 0) Reset();
            else foreach (int remaining in wins) { PendingWin = remaining; break; }
            return action;
        }
        if (down)
        {
            cancelled = true;
            if (!replayed && wins.Count == 1) return FullscreenWinAction.ReplayChord;
        }
        return FullscreenWinAction.Pass;
    }
    public void MarkReplayed() { replayed = true; cancelled = true; }
    public void Cancel() { cancelled = true; }
    public void Reset() { wins.Clear(); PendingWin = 0; cancelled = replayed = false; }
}
