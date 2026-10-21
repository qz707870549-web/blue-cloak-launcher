using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

namespace Luma;

public static class FluentTheme
{
	private class ThemeColor : INotifyPropertyChanged
	{
		private Color color;

		public Color Value
		{
			get
			{
				return color;
			}
			set
			{
				color = value;
				if (this.PropertyChanged != null)
				{
					this.PropertyChanged(this, new PropertyChangedEventArgs("Value"));
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public ThemeColor(Color value)
		{
			color = value;
		}
	}

	public static readonly SolidColorBrush Background;

	public static readonly SolidColorBrush Sidebar;

	public static readonly SolidColorBrush Card;

	public static readonly SolidColorBrush Line;

	public static readonly SolidColorBrush Selected;

	public static bool Dark;

	private static readonly Dictionary<Brush, ThemeColor> colors;

	public static bool Motion
	{
		get
		{
			if (SystemParameters.ClientAreaAnimation)
			{
				return !((Application.Current as Program)?.State.ReduceMotion ?? false);
			}
			return false;
		}
	}

	static FluentTheme()
	{
		Background = UI.Brush("#FAFAFC") as SolidColorBrush;
		Sidebar = UI.Brush("#FFFFFF") as SolidColorBrush;
		Card = UI.Brush("#FFFFFF") as SolidColorBrush;
		Line = UI.Brush("#E5E5EA") as SolidColorBrush;
		Selected = UI.Brush("#F2EEF9") as SolidColorBrush;
		colors = new Dictionary<Brush, ThemeColor>();
		Brush[] array = new Brush[9]
		{
			Background,
			Sidebar,
			Card,
			Line,
			Selected,
			UI.Ink,
			UI.Muted,
			UI.Accent,
			UI.Panel
		};
		foreach (Brush brush in array)
		{
			ThemeColor themeColor = new ThemeColor(((SolidColorBrush)brush).Color);
			colors[brush] = themeColor;
			BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty, new Binding("Value")
			{
				Source = themeColor
			});
		}
	}

	public static bool IsDark(string mode)
	{
		if (mode == "深色")
		{
			return true;
		}
		if (mode == "浅色")
		{
			return false;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
			return registryKey != null && Convert.ToInt32(registryKey.GetValue("AppsUseLightTheme", 1)) == 0;
		}
		catch
		{
			return false;
		}
	}

	private static void Set(Brush brush, string color)
	{
		colors[brush].Value = (Color)ColorConverter.ConvertFromString(color);
	}

	public static void Apply(string mode)
	{
		Dark = IsDark(mode);
		Set(Background, Dark ? "#000000" : "#FAFAFC");
		Set(Sidebar, Dark ? "#171719" : "#F8F8FA");
		Set(Card, Dark ? "#1C1C1C" : "#FFFFFF");
		Set(Line, Dark ? "#363636" : "#E5E5EA");
		Set(Selected, Dark ? "#303030" : "#EEEEEE");
		Set(UI.Ink, Dark ? "#EEEAF4" : "#1D1D1F");
		Set(UI.Muted, Dark ? "#C0B8CC" : "#777982");
		Set(UI.Accent, Dark ? "#B8B8B8" : "#666666");
		Set(UI.Panel, Dark ? "#1C1C1C" : "#FAFAFC");
		ConfigureResources();
	}

	private static void ConfigureResources()
	{
		Application current = Application.Current;
		if (current != null)
		{
			ResourceDictionary resources = current.Resources;
			resources["Luma.Selection"] = Selected;
			resources["Luma.Accent"] = UI.Accent;
			resources["Luma.Card"] = Card;
			resources["Luma.Ink"] = UI.Ink;
			resources["Luma.Line"] = Line;
			resources["Luma.Muted"] = UI.Muted;
			resources[SystemColors.WindowBrushKey] = Card;
			resources[SystemColors.WindowTextBrushKey] = UI.Ink;
			resources[SystemColors.ControlBrushKey] = Card;
			resources[SystemColors.ControlTextBrushKey] = UI.Ink;
			resources[typeof(ScrollBar)] = XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'><Setter Property='Width' Value='8'/><Setter Property='Margin' Value='2,0'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='{TemplateBinding Orientation}' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}'><Track.Thumb><Thumb MinHeight='24' Opacity='0.45'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Width='6' HorizontalAlignment='Center' Background='{DynamicResource Luma.Muted}' CornerRadius='3'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></ControlTemplate></Setter.Value></Setter></Style>");
			Style style = new Style(typeof(TextBox));
			style.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("Luma.Card")));
			style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("Luma.Ink")));
			style.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("Luma.Line")));
			style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1.0)));
			resources[typeof(TextBox)] = style;
			Type[] array = new Type[3]
			{
				typeof(ComboBox),
				typeof(ContextMenu),
				typeof(MenuItem)
			};
			foreach (Type type in array)
			{
				Style style2 = new Style(type);
				style2.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("Luma.Card")));
				style2.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("Luma.Ink")));
				resources[type] = style2;
			}
			resources.Remove(typeof(TextBox));
			resources.Remove(typeof(ComboBox));
			resources.Remove(typeof(ContextMenu));
			resources.Remove(typeof(MenuItem));
			AirStyle.Resources(resources);
		}
	}
}
