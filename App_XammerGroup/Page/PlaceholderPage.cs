using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace App_XammerGroup
{
    public class PlaceholderPage : Page
    {
        public PlaceholderPage(string title, string description)
        {
            Background = Brushes.White;
            Content = BuildContent(title, description);
        }

        private static UIElement BuildContent(string title, string description)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(32),
                VerticalAlignment = VerticalAlignment.Top
            };

            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 28,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A"))
            });

            panel.Children.Add(new TextBlock
            {
                Text = description,
                Margin = new Thickness(0, 12, 0, 0),
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DimGray
            });

            return panel;
        }
    }
}
