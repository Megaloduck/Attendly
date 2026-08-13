using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Attendly.Controls;

/// <summary>
/// Minimal ring/progress indicator for the Dashboard's "assigned to Kelas" stat -
/// no charting package needed for one number, so this hand-draws the ring instead
/// of pulling in a dependency. Percentage is 0-100; the arc starts at 12 o'clock
/// and sweeps clockwise, which is how most people read a progress ring.
/// </summary>
public class DonutProgress : Control
{
    public static readonly StyledProperty<double> PercentageProperty =
        AvaloniaProperty.Register<DonutProgress, double>(nameof(Percentage));

    public static readonly StyledProperty<IBrush> TrackBrushProperty =
        AvaloniaProperty.Register<DonutProgress, IBrush>(nameof(TrackBrush), new SolidColorBrush(Color.Parse("#E4DDD0")));

    public static readonly StyledProperty<IBrush> ProgressBrushProperty =
        AvaloniaProperty.Register<DonutProgress, IBrush>(nameof(ProgressBrush), new SolidColorBrush(Color.Parse("#BD5B3D")));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<DonutProgress, double>(nameof(StrokeThickness), 14.0);

    public double Percentage
    {
        get => GetValue(PercentageProperty);
        set => SetValue(PercentageProperty, value);
    }

    public IBrush TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush ProgressBrush
    {
        get => GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    static DonutProgress()
    {
        AffectsRender<DonutProgress>(PercentageProperty, TrackBrushProperty, ProgressBrushProperty, StrokeThicknessProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var thickness = StrokeThickness;
        var radius = (size - thickness) / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);

        var trackPen = new Pen(TrackBrush, thickness, lineCap: PenLineCap.Round);
        context.DrawEllipse(null, trackPen, center, radius, radius);

        var pct = Math.Clamp(Percentage, 0, 100);
        if (pct <= 0) return;

        var progressPen = new Pen(ProgressBrush, thickness, lineCap: PenLineCap.Round);

        // A true 360deg sweep has coincident start/end points, which ArcSegment
        // can't express - draw a full ring directly instead in that case.
        if (pct >= 100)
        {
            context.DrawEllipse(null, progressPen, center, radius, radius);
            return;
        }

        var sweepAngle = pct / 100.0 * 360.0;
        var startPoint = PointOnCircle(center, radius, 0);
        var endPoint = PointOnCircle(center, radius, sweepAngle);

        var figure = new PathFigure
        {
            StartPoint = startPoint,
            IsClosed = false,
            Segments = new PathSegments
            {
                new ArcSegment
                {
                    Point = endPoint,
                    Size = new Size(radius, radius),
                    IsLargeArc = sweepAngle > 180.0,
                    SweepDirection = SweepDirection.Clockwise,
                }
            }
        };

        var geometry = new PathGeometry { Figures = new PathFigures { figure } };
        context.DrawGeometry(null, progressPen, geometry);
    }

    /// <summary>Point on the circle at angleDegrees measured clockwise from 12 o'clock.</summary>
    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = (angleDegrees - 90) * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}