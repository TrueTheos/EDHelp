using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using EDHelp.ViewModels;

namespace EDHelp.Controls;

public partial class ManaCurveChart : UserControl
{
    public static readonly StyledProperty<ObservableCollection<ManaCurvePoint>?> ManaCurveProperty =
        AvaloniaProperty.Register<ManaCurveChart, ObservableCollection<ManaCurvePoint>?>(nameof(ManaCurve));

    public ObservableCollection<ManaCurvePoint>? ManaCurve
    {
        get => GetValue(ManaCurveProperty);
        set => SetValue(ManaCurveProperty, value);
    }

    public ManaCurveChart()
    {
        InitializeComponent();
        PropertyChanged += (sender, e) =>
        {
            if (e.Property == ManaCurveProperty)
            {
                DrawChart();
            }
        };
        
        SizeChanged += (sender, e) => DrawChart();
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();
        
        if (ManaCurve == null || !ManaCurve.Any())
            return;

        var actualWidth = Bounds.Width > 0 ? Bounds.Width : 260;
        var actualHeight = Bounds.Height > 0 ? Bounds.Height : 100;
        
        if (actualWidth <= 0 || actualHeight <= 0)
            return;

        var maxCount = ManaCurve.Max(p => p.count);
        if (maxCount == 0) maxCount = 1; // Avoid division by zero

        var barWidth = (actualWidth - 40) / ManaCurve.Count; // 40px for margins
        var chartHeight = actualHeight - 40; // 40px for labels and margins

        for (int i = 0; i < ManaCurve.Count; i++)
        {
            var point = ManaCurve[i];
            var barHeight = (double)point.count / maxCount * chartHeight;
            var x = 20 + i * barWidth + barWidth * 0.1; // 20px left margin, 10% spacing
            var y = 20 + chartHeight - barHeight; // 20px top margin
            var width = barWidth * 0.8; // 80% of available width

            // Create bar
            var bar = new Rectangle
            {
                Width = width,
                Height = barHeight,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromRgb(100, 149, 237), 0),
                        new GradientStop(Color.FromRgb(65, 105, 225), 1)
                    }
                },
                Stroke = Brushes.DarkBlue,
                StrokeThickness = 1
            };
            
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y);
            ChartCanvas.Children.Add(bar);

            // Create mana cost label
            var costLabel = new TextBlock
            {
                Text = point.manaCost,
                FontSize = 10,
                Foreground = Brushes.White,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            
            Canvas.SetLeft(costLabel, x + width / 2 - 5);
            Canvas.SetTop(costLabel, 20 + chartHeight + 5);
            ChartCanvas.Children.Add(costLabel);

            // Create count label on top of bar
            if (point.count > 0)
            {
                var countLabel = new TextBlock
                {
                    Text = point.count.ToString(),
                    FontSize = 9,
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                
                Canvas.SetLeft(countLabel, x + width / 2 - 4);
                Canvas.SetTop(countLabel, y - 15);
                ChartCanvas.Children.Add(countLabel);
            }
        }
    }
}