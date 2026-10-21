using System;
using Luma;
class FullscreenWinKeyTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static FullscreenWinAction Key(FullscreenWinKeyPolicy p, int key, bool down, bool enabled = true, bool fullscreen = true, bool modifier = false, bool injected = false) => p.Process(key, down, injected, enabled, fullscreen, modifier);
    [STAThread] static int Main()
    {
        try
        {
            foreach (int win in new[] { 0x5b, 0x5c })
            {
                var p = new FullscreenWinKeyPolicy();
                Check(Key(p, win, true) == FullscreenWinAction.Suppress && Key(p, win, true) == FullscreenWinAction.Suppress && Key(p, win, false) == FullscreenWinAction.Toggle && !p.IsHandling, "fullscreen bare Win suppresses native Start and toggles once without input injection: " + win);
                p = new FullscreenWinKeyPolicy();
                Check(Key(p, win, true, fullscreen:false) == FullscreenWinAction.Pass && Key(p, win, false, fullscreen:false) == FullscreenWinAction.Pass, "windowed input stays with existing Win-key policy: " + win);
                p = new FullscreenWinKeyPolicy();
                Check(Key(p, win, true, enabled:false) == FullscreenWinAction.Pass && Key(p, win, false, enabled:false) == FullscreenWinAction.Pass, "disabled Win replacement leaves physical keys untouched: " + win);
                p = new FullscreenWinKeyPolicy();
                Check(Key(p, win, true, modifier:true) == FullscreenWinAction.Pass, "preheld modifier remains native: " + win);
                p = new FullscreenWinKeyPolicy();Key(p, 0x57, true);
                Check(Key(p, win, true) == FullscreenWinAction.Suppress && Key(p, win, false) == FullscreenWinAction.Toggle, "holding game movement before Win does not prevent launcher: " + win);
                foreach (int chord in new[] { 0x45, 0x52, 0x44, 0x47, 0x10, 0x11, 0x4c })
                {
                    p = new FullscreenWinKeyPolicy();Key(p, win, true);
                    Check(Key(p, chord, true) == FullscreenWinAction.ReplayChord && p.PendingWin == win, "fullscreen chord requests ordered replay: " + win + "/" + chord);
                    p.MarkReplayed();
                    Check(Key(p, chord, false) == FullscreenWinAction.Pass && Key(p, win, false) == FullscreenWinAction.Pass && !p.IsHandling, "replayed chord releases pass through without opening launcher: " + win + "/" + chord);
                }
                p = new FullscreenWinKeyPolicy();Key(p, win, true);Key(p, 0x45, true);
                Check(Key(p, win, false) == FullscreenWinAction.Suppress && !p.IsHandling, "blocked injection cannot leave Win held or open native Start: " + win);
                p = new FullscreenWinKeyPolicy();Key(p, win, true);p.Cancel();
                Check(Key(p, win, false) == FullscreenWinAction.Suppress, "mouse interaction cancels pending single-key launcher: " + win);
                p = new FullscreenWinKeyPolicy();Key(p, win, true);
                Check(Key(p, win, false, enabled:false) == FullscreenWinAction.Suppress, "disabled mid-press consumes only the already buffered release: " + win);
                p = new FullscreenWinKeyPolicy();Key(p, win, true);
                Check(Key(p, 0xe8, true, injected:true) == FullscreenWinAction.Pass && Key(p, win, false, fullscreen:false) == FullscreenWinAction.Toggle, "injected input and focus changes cannot break a buffered gesture: " + win);
                p = new FullscreenWinKeyPolicy();Key(p, win, true);p.Reset();
                Check(!p.IsHandling && Key(p, win, false) == FullscreenWinAction.Pass, "recovery reset clears stale capture: " + win);
            }
            var dual = new FullscreenWinKeyPolicy();Key(dual, 0x5b, true);Key(dual, 0x5c, true);
            Check(Key(dual, 0x5b, false) == FullscreenWinAction.Suppress && Key(dual, 0x5c, false) == FullscreenWinAction.Suppress && !dual.IsHandling, "dual Win presses neither open native Start nor toggle launcher");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
