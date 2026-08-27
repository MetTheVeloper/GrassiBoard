using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GrassiBoard.Models.Looper;

namespace GrassiBoard.Views.Looper;

internal sealed class WaveformView : FrameworkElement
{
    private enum DragMode { None, Start, End, Playhead, Pan }

    private const double HandleHitPixels = 12.0;
    private const double MaxZoom = 128.0;
    private DragMode _dragMode;
    private double _viewStart;
    private double _viewEnd = 1.0;
    private double _panAnchorX;
    private double _panStartViewStart;
    private double _previewPlayhead = double.NaN;

    public static readonly DependencyProperty WaveformDataProperty = DependencyProperty.Register(
        nameof(WaveformData), typeof(WaveformEnvelope), typeof(WaveformView),
        new FrameworkPropertyMetadata(WaveformEnvelope.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnWaveformChanged));

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

    public static readonly DependencyProperty IsSeekableProperty = DependencyProperty.Register(
        nameof(IsSeekable), typeof(bool), typeof(WaveformView), new PropertyMetadata(false));

    public static readonly DependencyProperty SeekCommandProperty = DependencyProperty.Register(
        nameof(SeekCommand), typeof(ICommand), typeof(WaveformView), new PropertyMetadata(null));

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
    public bool IsSeekable { get => (bool)GetValue(IsSeekableProperty); set => SetValue(IsSeekableProperty, value); }
    public ICommand? SeekCommand { get => (ICommand?)GetValue(SeekCommandProperty); set => SetValue(SeekCommandProperty, value); }
    public bool IsRecording { get => (bool)GetValue(IsRecordingProperty); set => SetValue(IsRecordingProperty, value); }
    public bool IsArmed { get => (bool)GetValue(IsArmedProperty); set => SetValue(IsArmedProperty, value); }
    public bool ShowTrimHandles { get => (bool)GetValue(ShowTrimHandlesProperty); set => SetValue(ShowTrimHandlesProperty, value); }
    public bool CompactMode { get => (bool)GetValue(CompactModeProperty); set => SetValue(CompactModeProperty, value); }

    public double ZoomFactor => 1.0 / Math.Max(1.0 / MaxZoom, _viewEnd - _viewStart);

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
            DrawEnvelope(drawingContext, envelope, width, height);
        }

        double selectionStart = Math.Clamp(SelectionStart, 0.0, 1.0);
        double selectionEnd = Math.Clamp(SelectionEnd, selectionStart, 1.0);
        var dimBrush = new SolidColorBrush(Color.FromArgb(112, 0, 0, 0));
        double selectionStartX = NormalizedToX(selectionStart, width);
        double selectionEndX = NormalizedToX(selectionEnd, width);
        if (selectionStart > _viewStart)
        {
            drawingContext.DrawRectangle(dimBrush, null, new Rect(0, 0, Math.Clamp(selectionStartX, 0.0, width), height));
        }
        if (selectionEnd < _viewEnd)
        {
            double rightStart = Math.Clamp(selectionEndX, 0.0, width);
            drawingContext.DrawRectangle(dimBrush, null, new Rect(rightStart, 0, width - rightStart, height));
        }

        if (ShowTrimHandles)
        {
            var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 214, 92)), 2.0);
            if (selectionStart >= _viewStart && selectionStart <= _viewEnd)
            {
                drawingContext.DrawLine(handlePen, new Point(selectionStartX, 0), new Point(selectionStartX, height));
            }
            if (selectionEnd >= _viewStart && selectionEnd <= _viewEnd)
            {
                drawingContext.DrawLine(handlePen, new Point(selectionEndX, 0), new Point(selectionEndX, height));
            }
        }

        double playhead = double.IsFinite(_previewPlayhead) ? _previewPlayhead : PlayheadPosition;
        if (double.IsFinite(playhead) && playhead >= _viewStart && playhead <= _viewEnd)
        {
            double playheadX = NormalizedToX(Math.Clamp(playhead, 0.0, 1.0), width);
            drawingContext.DrawLine(new Pen(Brushes.White, 1.25), new Point(playheadX, 0), new Point(playheadX, height));
        }

        if (ZoomFactor > 1.001)
        {
            var label = new FormattedText(
                $"{ZoomFactor:0.#}×",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 10.0, Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(label, new Point(Math.Max(4.0, width - label.Width - 6.0), 4.0));
        }
    }

    private void DrawEnvelope(DrawingContext drawingContext, WaveformEnvelope envelope, double width, double height)
    {
        var waveformBrush = new SolidColorBrush(WaveformColor);
        waveformBrush.Freeze();
        var waveformPen = new Pen(waveformBrush, CompactMode ? 1.0 : 1.25);
        waveformPen.Freeze();
        double halfHeight = height * 0.46;
        double middle = height / 2.0;
        int count = envelope.Count;
        int first = Math.Clamp((int)Math.Floor(_viewStart * count), 0, Math.Max(0, count - 1));
        int lastExclusive = Math.Clamp((int)Math.Ceiling(_viewEnd * count), first + 1, count);
        int visibleCount = lastExclusive - first;
        int columns = Math.Max(1, Math.Min(visibleCount, (int)Math.Ceiling(width)));

        for (int column = 0; column < columns; column++)
        {
            int bucketStart = first + (int)((long)column * visibleCount / columns);
            int bucketEnd = first + (int)((long)(column + 1) * visibleCount / columns);
            if (bucketEnd <= bucketStart) bucketEnd = Math.Min(lastExclusive, bucketStart + 1);
            float minimum = 0.0F;
            float maximum = 0.0F;
            bool initialized = false;
            for (int bucket = bucketStart; bucket < bucketEnd; bucket++)
            {
                float lo = envelope.Minimum[bucket];
                float hi = envelope.Maximum[bucket];
                if (!initialized)
                {
                    minimum = lo;
                    maximum = hi;
                    initialized = true;
                }
                else
                {
                    minimum = Math.Min(minimum, lo);
                    maximum = Math.Max(maximum, hi);
                }
            }
            double normalized = ((bucketStart + bucketEnd) * 0.5) / count;
            double x = NormalizedToX(normalized, width);
            double top = middle - Math.Clamp(maximum, -1.0F, 1.0F) * halfHeight;
            double bottom = middle - Math.Clamp(minimum, -1.0F, 1.0F) * halfHeight;
            drawingContext.DrawLine(waveformPen, new Point(x, top), new Point(x, bottom));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (ActualWidth <= 1.0) return;
        double x = e.GetPosition(this).X;

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && ZoomFactor > 1.001)
        {
            BeginPan(x);
            e.Handled = true;
            return;
        }

        if (IsEditable)
        {
            double startX = NormalizedToX(Math.Clamp(SelectionStart, 0.0, 1.0), ActualWidth);
            double endX = NormalizedToX(Math.Clamp(SelectionEnd, 0.0, 1.0), ActualWidth);
            double startDistance = SelectionStart >= _viewStart && SelectionStart <= _viewEnd ? Math.Abs(x - startX) : double.MaxValue;
            double endDistance = SelectionEnd >= _viewStart && SelectionEnd <= _viewEnd ? Math.Abs(x - endX) : double.MaxValue;
            if (Math.Min(startDistance, endDistance) <= HandleHitPixels)
            {
                _dragMode = startDistance <= endDistance ? DragMode.Start : DragMode.End;
                CaptureMouse();
                ApplyTrimPointer(x);
                e.Handled = true;
                return;
            }
        }

        if (IsSeekable)
        {
            _dragMode = DragMode.Playhead;
            _previewPlayhead = XToNormalized(x);
            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Middle && ZoomFactor > 1.001)
        {
            BeginPan(e.GetPosition(this).X);
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragMode == DragMode.None) return;
        double x = e.GetPosition(this).X;
        switch (_dragMode)
        {
            case DragMode.Start:
            case DragMode.End:
                if (e.LeftButton == MouseButtonState.Pressed) ApplyTrimPointer(x);
                break;
            case DragMode.Playhead:
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    _previewPlayhead = XToNormalized(x);
                    InvalidateVisual();
                }
                break;
            case DragMode.Pan:
                if (e.LeftButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed)
                {
                    double span = _viewEnd - _viewStart;
                    double delta = (x - _panAnchorX) / Math.Max(1.0, ActualWidth) * span;
                    SetView(_panStartViewStart - delta, span);
                }
                break;
        }
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragMode == DragMode.None) return;
        double x = e.GetPosition(this).X;
        if (_dragMode is DragMode.Start or DragMode.End)
        {
            ApplyTrimPointer(x);
        }
        else if (_dragMode == DragMode.Playhead)
        {
            double target = XToNormalized(x);
            _previewPlayhead = double.NaN;
            if (SeekCommand?.CanExecute(target) == true) SeekCommand.Execute(target);
        }
        EndDrag();
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton == MouseButton.Middle && _dragMode == DragMode.Pan)
        {
            EndDrag();
            e.Handled = true;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (ActualWidth <= 1.0) return;
        double anchor = XToNormalized(e.GetPosition(this).X);
        double span = _viewEnd - _viewStart;
        double targetSpan = e.Delta > 0 ? span / 1.6 : span * 1.6;
        targetSpan = Math.Clamp(targetSpan, 1.0 / MaxZoom, 1.0);
        double ratio = (anchor - _viewStart) / Math.Max(1e-9, span);
        SetView(anchor - ratio * targetSpan, targetSpan);
        e.Handled = true;
    }

    public void ZoomIn() => ZoomAround((_viewStart + _viewEnd) * 0.5, 1.0 / 1.8);
    public void ZoomOut() => ZoomAround((_viewStart + _viewEnd) * 0.5, 1.8);
    public void ResetZoom() => SetView(0.0, 1.0);

    public void ZoomToSelection()
    {
        double start = Math.Clamp(SelectionStart, 0.0, 1.0);
        double end = Math.Clamp(SelectionEnd, start, 1.0);
        double span = Math.Max(1.0 / MaxZoom, end - start);
        double margin = span * 0.08;
        span = Math.Min(1.0, span + margin * 2.0);
        SetView((start + end) * 0.5 - span * 0.5, span);
    }

    private void ZoomAround(double anchor, double multiplier)
    {
        double span = Math.Clamp((_viewEnd - _viewStart) * multiplier, 1.0 / MaxZoom, 1.0);
        SetView(anchor - span * 0.5, span);
    }

    private void BeginPan(double x)
    {
        _dragMode = DragMode.Pan;
        _panAnchorX = x;
        _panStartViewStart = _viewStart;
        CaptureMouse();
    }

    private void EndDrag()
    {
        _dragMode = DragMode.None;
        _previewPlayhead = double.NaN;
        if (IsMouseCaptured) ReleaseMouseCapture();
        InvalidateVisual();
    }

    private void ApplyTrimPointer(double x)
    {
        double normalized = XToNormalized(x);
        const double minimumGap = 0.00001;
        if (_dragMode == DragMode.Start)
        {
            SelectionStart = Math.Min(normalized, Math.Max(0.0, SelectionEnd - minimumGap));
        }
        else if (_dragMode == DragMode.End)
        {
            SelectionEnd = Math.Max(normalized, Math.Min(1.0, SelectionStart + minimumGap));
        }
    }

    private double XToNormalized(double x)
    {
        double ratio = Math.Clamp(x / Math.Max(1.0, ActualWidth), 0.0, 1.0);
        return _viewStart + ratio * (_viewEnd - _viewStart);
    }

    private double NormalizedToX(double normalized, double width) =>
        (normalized - _viewStart) / Math.Max(1e-9, _viewEnd - _viewStart) * width;

    private void SetView(double start, double span)
    {
        span = Math.Clamp(span, 1.0 / MaxZoom, 1.0);
        start = Math.Clamp(start, 0.0, 1.0 - span);
        _viewStart = start;
        _viewEnd = start + span;
        InvalidateVisual();
    }

    private static void OnWaveformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WaveformView view) return;
        view._viewStart = 0.0;
        view._viewEnd = 1.0;
        view._previewPlayhead = double.NaN;
        view.InvalidateVisual();
    }
}