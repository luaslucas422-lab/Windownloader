using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDownloader.Converters;

namespace WinDownloader.Controls;

/// <summary>
/// The big "how updated are you" dial on the dashboard — a 270° arc that fills and recolours with the score.
/// Drawn directly in OnRender so it stays crisp at any DPI.
/// </summary>
public sealed class ScoreGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ScoreGauge), new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty AnimatedValueProperty = DependencyProperty.Register(
        nameof(AnimatedValue), typeof(double), typeof(ScoreGauge),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ScoreGauge),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HasValueProperty = DependencyProperty.Register(
        nameof(HasValue), typeof(bool), typeof(ScoreGauge),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double AnimatedValue { get => (double)GetValue(AnimatedValueProperty); set => SetValue(AnimatedValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool HasValue { get => (bool)GetValue(HasValueProperty); set => SetValue(HasValueProperty, value); }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var g = (ScoreGauge)d;
        var anim = new DoubleAnimation(Math.Clamp((double)e.NewValue, 0, 100), TimeSpan.FromMilliseconds(900))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        g.BeginAnimation(AnimatedValueProperty, anim);
    }

    private const double StartAngle = 135;   // degrees, clockwise from 3 o'clock
    private const double Sweep = 270;

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double size = Math.Min(w, h);
        double thickness = Math.Max(8, size * 0.085);
        double radius = size / 2 - thickness / 2 - 2;
        var center = new Point(w / 2, h / 2);

        var track = new Pen(new SolidColorBrush(Color.FromRgb(0x27, 0x34, 0x49)), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, track, Arc(center, radius, StartAngle, Sweep));

        double v = Math.Clamp(AnimatedValue, 0, 100);
        var brush = Palette.ForScore(HasValue ? (int)Math.Round(v) : -1);
        if (HasValue && v > 0)
        {
            var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, pen, Arc(center, radius, StartAngle, Sweep * v / 100));
        }

        // Centre text
        double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        string big = HasValue ? ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture) : "–";
        var bigText = new FormattedText(big, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            size * 0.26, Brushes.White, dip);
        dc.DrawText(bigText, new Point(center.X - bigText.Width / 2, center.Y - bigText.Height / 2 - size * 0.04));

        var pct = new FormattedText("/100", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size * 0.07, Palette.Muted, dip);
        dc.DrawText(pct, new Point(center.X - pct.Width / 2, center.Y + bigText.Height / 2 - size * 0.07));

        if (!string.IsNullOrEmpty(Label))
        {
            var label = new FormattedText(Label.ToUpperInvariant(), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                size * 0.055, brush, dip);
            dc.DrawText(label, new Point(center.X - label.Width / 2, center.Y + radius * 0.78));
        }
    }

    private static Geometry Arc(Point c, double r, double startDeg, double sweepDeg)
    {
        sweepDeg = Math.Max(0.01, Math.Min(359.99, sweepDeg));
        var start = PointAt(c, r, startDeg);
        var end = PointAt(c, r, startDeg + sweepDeg);
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }

    private static Point PointAt(Point c, double r, double deg)
    {
        double rad = deg * Math.PI / 180;
        return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
    }
}
