using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Luma;

public static class WorkspaceCards
{
    public static Grid Minimal(Config config, string group)
    {
        var face = new Grid { Height = 144, UseLayoutRounding = true, SnapsToDevicePixels = true };
        var body = new Grid { Margin = new Thickness(18), IsHitTestVisible = false };
        var title = new DockPanel { VerticalAlignment = VerticalAlignment.Top, LastChildFill = true };
        var symbol = group == "工具" ? AirStyle.Icon("tool") : NavigationIcons.Create(group);
        symbol.Width = 20; symbol.Height = 20; symbol.Margin = new Thickness(0, 0, 10, 0);
        title.Children.Add(symbol);
        var name = UI.Text(Config.GroupDisplayName(group), 16, UI.Ink);
        name.FontWeight = FontWeights.SemiBold;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        title.Children.Add(name);
        body.Children.Add(title);
        var icons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Tag = "workspace-card-preview" };
        foreach (var app in config.OrderedEntries(config.Apps.Where(a => config.IsVisible(a) && config.LibraryGroup(a) == group)).Take(4))
        {
            var icon = UI.Icon(app, 32);
            icon.Tag = app;
            icon.Margin = new Thickness(0, 0, 10, 0);
            icons.Children.Add(icon);
        }
        body.Children.Add(icons);
        face.Children.Add(new Border { Background = FluentTheme.Card, BorderBrush = FluentTheme.Line,
            BorderThickness = new Thickness(0.7), CornerRadius = new CornerRadius(14), Child = body });
        return face;
    }
}
