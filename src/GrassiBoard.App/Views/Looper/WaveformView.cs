using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GrassiBoard.Models.Looper;

namespace GrassiBoard.Views.Looper;

internal sealed class WaveformView : FrameworkElement
{
    private enum DragHandle { None, Start, End }
    private DragHandle _dragHandle;

    public static readonly DependencyProperty WaveformDataProperty = DependencyProperty.Register(
        nameof(WaveformData), typeof(WaveformEnvelope), typeof(WaveformView),
        new FrameworkPropertyMetadata(WaveformEnvelope.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WaveformColorProperty = DependencyProperty.Register(
        nameof(WaveformColor), typeof(Color), typeof(WaveformView),
        new FrameworkPropertyMetadata(Color.FromRgb(86, 211, 163), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlayheadPositionProperty = DependencyProperty.Register(
        nameof(PlayheadPosition), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(-1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionStartProperty = DependencyProperty.Register(
        nameof(SelectionStart), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SelectionEndProperty = DependencyProperty.Register(
        nameof(SelectionEnd), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
        nameof(IsEditable), typeof(bool), typeof(WaveformView), new PropertyMetadata(false));

    public static readonly DependencyProperty IsRecordingProperty = DependencyProperty.Register(
        nameof(IsRecording), typeof(bool), typeof(WaveformView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsArmedProperty = DependencyProperty.Register(
        nameof(IsArmed), typeof(bool), typeof(WaveformView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowTrimHandlesProperty = DependencyProperty.Register(
        nameof(ShowTrimHandles), typeof(bool), typeof(WaveformView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CompactModeProperty = DependencyProperty.Register(
        nameof(CompactMode), typeof(bool), typeof(WaveformView), new PropertyMetadata(false));

    public WaveformEnvelope WaveformData { get => (WaveformEnvelope)GetValue(WaveformDataProperty); set => SetValue(WaveformDataProperty, value); }
    public Color WaveformColor { get => (Color)GetValue(WaveformColorProperty); set => SetValue(WaveformColorProperty, value); }
    public double PlayheadPosition { get => (double)GetValue(PlayheadPositionProperty); set => SetValue(PlayheadPositionProperty, value); }
    public double SelectionStart { get => (double)GetValue(SelectionStartProperty); set => SetValue(SelectionStartProperty, value); }
    public double SelectionEnd { get => (double)GetValue(SelectionEndProperty); set => SetValue(SelectionEndProperty, value); }
    public bool IsEditable { get => (bool)GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }
    public bool IsRecording { get => (bool)GetValue(IsRecordingProperty); set => SetValue(IsRecordingProperty, value); }
    public bool IsArmed { get => (bool)GetValue(IsArmedProperty); set => SetValue(IsArmedProperty, value); }
    public bool ShowTrimHandles { get => (bool)GetValue(ShowTrimHandlesProperty); set => SetValue(ShowTrimHandlesProperty, value); }
    public bool CompactMode { get => (bool)GetValue(CompactModeProperty); set => SetValue(CompactModeProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsInfinity(availableSize.Width) ? 320.0 : availableSize.Width,
        double.IsInfinity(availableSize.Height) ? (CompactMode ? 54.0 : 180.0) : availableSize.Height);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 1.0 || height <= 1.0) return;

        drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)), null, new Rect(0, 0, width, height));
        drawingContext.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 1.0), new Point(0, height / 2.0), new Point(width, height / 2.0));

        WaveformEnvelope envelope = WaveformData ?? WaveformEnvelope.Empty;
        if (envelope.Count > 0)
        {
            var waveformBrush = new SolidColorBrush(WaveformColor);
            waveformBrush.Freeze();
            var waveformPen = new Pen(waveformBrush, CompactMode ? 1.0 : 1.25);
            waveformPen.Freeze();
            double halfHeight = height * 0.46;
            double middle = height / 2.0;
            for (int index = 0; index < envelope.Count; index++)
            {
                double x = (index + 0.5) * width / envelope.Count;
                double top = middle - Math.Clamp(envelope.Maximum[index], -1.0F, 1.0F) * halfHeight;
                double bottom = middle - Math.Clamp(envelope.Minimum[index], -1.0F, 1.0F) * halfHeight;
                drawingContext.DrawLine(waveformPen, new Point(x, top), new Point(x, bottom));
            }
        }

        double selectionStart = Math.Clamp(SelectionStart, 0.0, 1.0);
        double selectionEnd = Math.Clamp(SelectionEnd, selectionStart, 1.0);
        var dimBrush = new SolidColorBrush(Color.FromArgb(112, 0, 0, 0));
        if (selectionStart > 0.0) drawingContext.DrawRectangle(dimBrush, null, new Rect(0, 0, selectionStart * width, height));
        if (selectionEnd < 1.0) drawingContext.DrawRectangle(dimBrush, null, new Rect(selectionEnd * width, 0, (1.0 - selectionEnd) * width, height));

        if (ShowTrimHandles)
        {
            var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 214, 92)), 2.0);
            drawingContext.DrawLine(handlePen, new Point(selectionStart * width, 0), new Point(selectionStart * width, height));
            drawingContext.DrawLine(handlePen, new Point(selectionEnd * width, 0), new Point(selectionEnd * width, height));
        }

        double playhead = PlayheadPosition;
        if (double.IsFinite(playhead) && playhead >= 0.0)
        {
            playhead = Math.Clamp(playhead, 0.0, 1.0);
            drawingContext.DrawLine(new Pen(Brushes.White, 1.0), new Point(playhead * width, 0), new Point(playhead * width, height));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsEditable || ActualWidth <= 1.0) return;
        double x = e.GetPosition(this).X;
        double startX = Math.Clamp(SelectionStart, 0.0, 1.0) * ActualWidth;
        double endX = Math.Clamp(SelectionEnd, 0.0, 1.0) * ActualWidth;
        _dragHandle = Math.Abs(x - startX) <= Math.Abs(x - endX) ? DragHandle.Start : DragHandle.End;
        CaptureMouse();
        ApplyPointer(x);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragHandle == DragHandle.None || e.LeftButton != MouseButtonState.Pressed) return;
        ApplyPointer(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragHandle == DragHandle.None) return;
        ApplyPointer(e.GetPosition(this).X);
        _dragHandle = DragHandle.None;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void ApplyPointer(double x)
    {
        double normalized = Math.Clamp(x / Math.Max(1.0, ActualWidth), 0.0, 1.0);
        const double minimumGap = 0.00001;
        if (_dragHandle == DragHandle.Start)
        {
            SelectionStart = Math.Min(normalized, Math.Max(0.0, SelectionEnd - minimumGap));
        }
        else if (_dragHandle == DragHandle.End)
        {
            SelectionEnd = Math.Max(normalized, Math.Min(1.0, SelectionStart + minimumGap));
        }
    }
}
