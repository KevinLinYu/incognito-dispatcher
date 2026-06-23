using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using S = IncognitoDispatcher.Properties.Strings;



using Shapes = System.Windows.Shapes;

namespace IncognitoDispatcher;

public partial class BrowserPickerWindow : Window
{
    public (string Name, string Path)? SelectedBrowser { get; private set; }

    public BrowserPickerWindow(List<(string Name, string Path)> browsers)
    {
        Title = S.PickerTitle;
        Width = 400;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow;
        Background = (Brush)Application.Current.FindResource("CardBg");

        var root = new StackPanel { Margin = new Thickness(24) };

        root.Children.Add(new TextBlock
        {
            Text = S.PickerTitle,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Margin = new Thickness(0, 0, 0, 2)
        });
        root.Children.Add(new TextBlock
        {
            Text = S.PickerSubtitle,
            FontSize = 12,
            Foreground = (Brush)Application.Current.FindResource("TextSecondary"),
            Margin = new Thickness(0, 0, 0, 16)
        });

        var listPanel = new StackPanel();
        int selected = 0;
        Border? selectedBorder = null;

        for (int i = 0; i < browsers.Count; i++)
        {
            var (name, path) = browsers[i];
            var idx = i;

            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 4),
                Tag = idx
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icon = new TextBlock
            {
                Text = GetBrowserEmoji(name),
                FontSize = 20,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(icon, 0);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            info.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A))
            });
            info.Children.Add(new TextBlock
            {
                Text = path,
                FontSize = 11,
                Foreground = (Brush)Application.Current.FindResource("TextSecondary"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(info, 1);

            row.Children.Add(icon);
            row.Children.Add(info);
            card.Child = row;

            if (i == 0)
            {
                card.BorderBrush = (Brush)Application.Current.FindResource("AccentBrush");
                card.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF6, 0xFC));
                selectedBorder = card;
            }

            card.MouseLeftButtonDown += (_, _) =>
            {
                if (selectedBorder != null)
                {
                    selectedBorder.BorderBrush = Brushes.Transparent;
                    selectedBorder.Background = Brushes.White;
                }
                card.BorderBrush = (Brush)Application.Current.FindResource("AccentBrush");
                card.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF6, 0xFC));
                selectedBorder = card;
                selected = idx;
            };

            card.MouseEnter += (_, _) =>
            {
                if (card != selectedBorder)
                    card.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
            };
            card.MouseLeave += (_, _) =>
            {
                if (card != selectedBorder)
                    card.Background = Brushes.White;
            };

            listPanel.Children.Add(card);
        }

        var scrollViewer = new ScrollViewer
        {
            Content = listPanel,
            MaxHeight = 320,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.Children.Add(scrollViewer);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var okBtn = new Button
        {
            Content = S.PickerConfirm,
            Style = (Style)Application.Current.FindResource("AccentBtn"),
            MinWidth = 80
        };
        okBtn.Click += (_, _) => { SelectedBrowser = browsers[selected]; DialogResult = true; };

        var cancelBtn = new Button
        {
            Content = S.PickerCancel,
            Style = (Style)Application.Current.FindResource("SecondaryBtn"),
            MinWidth = 80,
            Margin = new Thickness(8, 0, 0, 0)
        };
        cancelBtn.Click += (_, _) => DialogResult = false;

        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        root.Children.Add(btnPanel);

        Content = root;
    }

    private static string GetBrowserEmoji(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("chrome") || lower.Contains("canary")) return "\U0001F310";
        if (lower.Contains("edge")) return "\U0001F537";
        if (lower.Contains("firefox")) return "\U0001F98A";
        if (lower.Contains("brave")) return "\U0001F981";
        if (lower.Contains("opera")) return "\U0001F534";
        if (lower.Contains("vivaldi")) return "\U0001F7E0";
        if (lower.Contains("arc")) return "\U0001F311";
        return "\U0001F30D";
    }
}
