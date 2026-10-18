using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Luma;

public class ReorderPanel : WrapPanel
{
	private class Session
	{
		public string Token = Guid.NewGuid().ToString("N");

		public ReorderPanel Panel;

		public Entry Entry;

		public Button Button;

		public List<UIElement> Original;

		public bool Committed;
	}

	private class DragGhost : Adorner
	{
		private readonly VisualBrush brush;

		private Point point;

		public DragGhost(UIElement parent, UIElement icon)
			: base(parent)
		{
			base.IsHitTestVisible = false;
			brush = new VisualBrush(icon);
			base.Opacity = 0.9;
		}

		public void Move(Point value)
		{
			point = value;
			InvalidateVisual();
		}

		protected override void OnRender(DrawingContext context)
		{
			Rect rectangle = new Rect(point.X + 12.0, point.Y + 12.0, 68.0, 68.0);
			context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 245, 250, byte.MaxValue)), new Pen(Brushes.White, 1.0), rectangle, 16.0, 16.0);
			context.DrawRectangle(brush, null, new Rect(rectangle.Left + 6.0, rectangle.Top + 6.0, 56.0, 56.0));
		}
	}

	private const string Format = "Luma.CategoryReorder";

	private static Session current;

	private readonly Program program;

	private readonly Library library;

	private readonly Dictionary<UIElement, Point> positions = new Dictionary<UIElement, Point>();

	private Point start;

	private Button pressed;

	private DragGhost ghost;

	private AdornerLayer layer;

	private int lastScroll;

	public static void CommitCurrent()
	{
		if (current != null)
		{
			current.Committed = true;
		}
	}

	public static Entry DraggedEntry(IDataObject data)
	{
		if (current == null || data == null || !data.GetDataPresent("Luma.CategoryReorder", autoConvert: false))
		{
			return null;
		}
		if (!string.Equals(data.GetData("Luma.CategoryReorder", autoConvert: false) as string, current.Token, StringComparison.Ordinal))
		{
			return null;
		}
		return current.Entry;
	}

	public static bool MoveDraggedEntry(IDataObject data, Program owner, string group)
	{
		Entry entry = DraggedEntry(data);
		if (entry == null || !owner.State.MoveEntryToGroup(entry, group))
		{
			return false;
		}
		current.Committed = true;
		owner.Save();
		owner.Library?.Render();
		return true;
	}

	public static void AttachExternalSource(Button button, Entry app, Program owner, Library window)
	{
		Point origin = default(Point);
		bool pressedHere = false;
		button.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs input)
		{
			origin = input.GetPosition(button);
			pressedHere = true;
		};
		button.PreviewMouseLeftButtonUp += delegate
		{
			pressedHere = false;
		};
		button.PreviewMouseMove += delegate(object sender, MouseEventArgs input)
		{
			if (pressedHere && !window.IsSelecting && input.LeftButton == MouseButtonState.Pressed && current == null && !(VisualTreeHelper.GetParent(button) is ReorderPanel))
			{
				Point position = input.GetPosition(button);
				if (!(Math.Abs(position.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance) || !(Math.Abs(position.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance))
				{
					pressedHere = false;
					input.Handled = true;
					button.ReleaseMouseCapture();
					current = new Session
					{
						Entry = app,
						Button = button
					};
					window.SuppressShortcutClick();
					window.SetReordering(value: true);
					button.Opacity = 0.35;
					try
					{
						DragDrop.DoDragDrop(button, new DataObject("Luma.CategoryReorder", current.Token), DragDropEffects.Move);
					}
					finally
					{
						button.Opacity = 1.0;
						current = null;
						window.SuppressShortcutClick();
						window.SetReordering(value: false);
					}
				}
			}
		};
	}

	public ReorderPanel(Program owner, Library window)
	{
		program = owner;
		library = window;
		base.AllowDrop = true;
		base.DragOver += OnDragOver;
		base.Drop += OnDrop;
	}

	public void AddApp(Entry app)
	{
		Button button = UI.AppButton(program, app, label: true);
		button.Tag = app;
		button.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs input)
		{
			start = input.GetPosition(this);
			pressed = button;
		};
		button.PreviewMouseLeftButtonUp += delegate
		{
			pressed = null;
		};
		button.PreviewMouseMove += delegate(object sender, MouseEventArgs input)
		{
			if (!library.IsSelecting && pressed == button && input.LeftButton == MouseButtonState.Pressed && current == null)
			{
				Point position = input.GetPosition(this);
				if (!(Math.Abs(position.X - start.X) < SystemParameters.MinimumHorizontalDragDistance) || !(Math.Abs(position.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance))
				{
					input.Handled = true;
					StartDrag(button, app);
				}
			}
		};
		base.Children.Add(button);
	}

	private static TranslateTransform Translation(UIElement child)
	{
		TransformGroup transformGroup = child.RenderTransform as TransformGroup;
		if (transformGroup == null)
		{
			transformGroup = new TransformGroup();
			if (child.RenderTransform != null && child.RenderTransform != Transform.Identity)
			{
				transformGroup.Children.Add(child.RenderTransform);
			}
			transformGroup.Children.Add(new TranslateTransform());
			child.RenderTransform = transformGroup;
		}
		TranslateTransform translateTransform = transformGroup.Children.OfType<TranslateTransform>().LastOrDefault();
		if (translateTransform == null)
		{
			translateTransform = new TranslateTransform();
			transformGroup.Children.Add(translateTransform);
		}
		return translateTransform;
	}

	protected override Size ArrangeOverride(Size size)
	{
		Size result = base.ArrangeOverride(size);
		foreach (UIElement child in base.Children)
		{
			if (child is FrameworkElement element)
			{
				Point location = LayoutInformation.GetLayoutSlot(element).Location;
				if (FluentTheme.Motion && positions.TryGetValue(child, out var value) && value != location)
				{
					TranslateTransform translateTransform = Translation(child);
					double fromValue = value.X + translateTransform.X - location.X;
					double fromValue2 = value.Y + translateTransform.Y - location.Y;
					CubicEase easingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					};
					translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromValue, 0.0, TimeSpan.FromMilliseconds(190.0))
					{
						EasingFunction = easingFunction,
						FillBehavior = FillBehavior.Stop
					});
					translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromValue2, 0.0, TimeSpan.FromMilliseconds(190.0))
					{
						EasingFunction = easingFunction,
						FillBehavior = FillBehavior.Stop
					});
				}
				positions[child] = location;
			}
		}
		foreach (UIElement item in positions.Keys.Where((UIElement child) => !base.Children.Contains(child)).ToList())
		{
			positions.Remove(item);
		}
		return result;
	}

	private void StartDrag(Button button, Entry app)
	{
		pressed = null;
		button.ReleaseMouseCapture();
		current = new Session
		{
			Panel = this,
			Entry = app,
			Button = button,
			Original = base.Children.Cast<UIElement>().ToList()
		};
		Session session = current;
		library.SetReordering(value: true);
		library.SuppressShortcutClick();
		button.Opacity = 0.28;
		layer = AdornerLayer.GetAdornerLayer(this);
		if (layer != null)
		{
			ghost = new DragGhost(this, UI.Icon(app, 56.0));
			layer.Add(ghost);
			ghost.Move(start);
		}
		try
		{
			DragDrop.DoDragDrop(button, new DataObject("Luma.CategoryReorder", session.Token), DragDropEffects.Move);
		}
		finally
		{
			if (!session.Committed)
			{
				base.Children.Clear();
				foreach (UIElement item in session.Original)
				{
					base.Children.Add(item);
				}
				InvalidateArrange();
			}
			button.Opacity = 1.0;
			if (layer != null && ghost != null)
			{
				layer.Remove(ghost);
			}
			ghost = null;
			layer = null;
			current = null;
			library.SuppressShortcutClick();
			library.SetReordering(value: false);
		}
	}

	private bool OwnDrag(DragEventArgs input)
	{
		if (current != null && current.Panel == this && input.Data.GetDataPresent("Luma.CategoryReorder", autoConvert: false))
		{
			return string.Equals(input.Data.GetData("Luma.CategoryReorder", autoConvert: false) as string, current.Token, StringComparison.Ordinal);
		}
		return false;
	}

	private void OnDragOver(object sender, DragEventArgs input)
	{
		if (!OwnDrag(input))
		{
			input.Effects = DragDropEffects.None;
			input.Handled = true;
			return;
		}
		Point position = input.GetPosition(this);
		if (ghost != null)
		{
			ghost.Move(position);
		}
		ScrollNearEdge(input);
		Button button = null;
		foreach (Button item in base.Children.OfType<Button>())
		{
			if (LayoutInformation.GetLayoutSlot(item).Contains(position))
			{
				button = item;
				break;
			}
		}
		if (button != null && button != current.Button)
		{
			if (!(button.Tag is Entry entry) || entry.TopPinned != current.Entry.TopPinned)
			{
				input.Effects = DragDropEffects.None;
				input.Handled = true;
				return;
			}
			Rect layoutSlot = LayoutInformation.GetLayoutSlot(button);
			bool flag = position.X >= layoutSlot.Left + layoutSlot.Width / 2.0;
			int num = base.Children.IndexOf(current.Button);
			int num2 = base.Children.IndexOf(button) + (flag ? 1 : 0);
			if (num < num2)
			{
				num2--;
			}
			if (num2 != num)
			{
				base.Children.Remove(current.Button);
				base.Children.Insert(num2, current.Button);
				UpdateLayout();
			}
		}
		input.Effects = DragDropEffects.Move;
		input.Handled = true;
	}

	private void OnDrop(object sender, DragEventArgs input)
	{
		if (OwnDrag(input))
		{
			List<Entry> reordered = (from button in base.Children.OfType<Button>()
				select button.Tag as Entry into entry
				where entry != null && entry.TopPinned == current.Entry.TopPinned
				select entry).ToList();
			if (program.State.SaveCategoryOrder(reordered))
			{
				current.Committed = true;
				program.Save();
				input.Effects = DragDropEffects.Move;
			}
			else
			{
				input.Effects = DragDropEffects.None;
			}
			input.Handled = true;
		}
	}

	private void ScrollNearEdge(DragEventArgs input)
	{
		if ((uint)(Environment.TickCount - lastScroll) < 60u)
		{
			return;
		}
		DependencyObject dependencyObject = this;
		while (dependencyObject != null && !(dependencyObject is ScrollViewer))
		{
			dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
		}
		if (dependencyObject is ScrollViewer scrollViewer)
		{
			double y = input.GetPosition(scrollViewer).Y;
			if (y < 32.0)
			{
				scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 20.0);
				lastScroll = Environment.TickCount;
			}
			else if (y > scrollViewer.ActualHeight - 32.0)
			{
				scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 20.0);
				lastScroll = Environment.TickCount;
			}
		}
	}
}
