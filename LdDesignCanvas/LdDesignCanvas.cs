using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LdDesignCanvas;

[TemplatePart(Name = PartViewport, Type = typeof(Border))]
[TemplatePart(Name = PartTopRuler, Type = typeof(Canvas))]
[TemplatePart(Name = PartLeftRuler, Type = typeof(Canvas))]
[TemplatePart(Name = PartHScrollBar, Type = typeof(ScrollBar))]
[TemplatePart(Name = PartVScrollBar, Type = typeof(ScrollBar))]
[TemplatePart(Name = PartLayoutCanvas, Type = typeof(Canvas))]
[TemplatePart(Name = PartPaperCanvas, Type = typeof(Canvas))]
public class LdDesignCanvas : Control
{
    private const string PartViewport = "PART_Viewport";
    private const string PartTopRuler = "PART_TopRuler";
    private const string PartLeftRuler = "PART_LeftRuler";
    private const string PartHScrollBar = "PART_HScrollBar";
    private const string PartVScrollBar = "PART_VScrollBar";
    private const string PartLayoutCanvas = "PART_LayoutCanvas";
    private const string PartPaperCanvas = "PART_PaperCanvas";

    private static readonly double[] MajorTickCandidates = [1d, 5d, 10d, 20d, 50d, 100d, 200d, 500d, 1000d];

    private Border? _viewport;
    private Canvas? _topRuler;
    private Canvas? _leftRuler;
    private ScrollBar? _horizontalScrollBar;
    private ScrollBar? _verticalScrollBar;
    private Canvas? _layoutCanvas;
    private Canvas? _paperCanvas;
    private Rectangle? _paperBorder;

    private bool _isApplyingScrollBarValue;
    private double _horizontalOffsetMm;
    private double _verticalOffsetMm;
    private double _minHorizontalOffsetMm;
    private double _maxHorizontalOffsetMm;
    private double _minVerticalOffsetMm;
    private double _maxVerticalOffsetMm;
    private double _lastScalePxPerMm = 96d / 25.4d;

    static LdDesignCanvas()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LdDesignCanvas), new FrameworkPropertyMetadata(typeof(LdDesignCanvas)));
    }

    public double PaperWidth => DesignWidth;

    public double PaperHeight => DesignHeight;

    public override void OnApplyTemplate()
    {
        UnhookTemplateEvents();

        base.OnApplyTemplate();

        _viewport = GetTemplateChild(PartViewport) as Border;
        _topRuler = GetTemplateChild(PartTopRuler) as Canvas;
        _leftRuler = GetTemplateChild(PartLeftRuler) as Canvas;
        _horizontalScrollBar = GetTemplateChild(PartHScrollBar) as ScrollBar;
        _verticalScrollBar = GetTemplateChild(PartVScrollBar) as ScrollBar;
        _layoutCanvas = GetTemplateChild(PartLayoutCanvas) as Canvas;
        _paperCanvas = GetTemplateChild(PartPaperCanvas) as Canvas;

        HookTemplateEvents();
        UpdateVisualState();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateVisualState();
    }

    protected override void OnPreviewMouseWheel(System.Windows.Input.MouseWheelEventArgs e)
    {
        if (!IsEnabled)
        {
            base.OnPreviewMouseWheel(e);
            return;
        }

        var zoomFactor = e.Delta > 0 ? 1.1d : 1d / 1.1d;
        ZoomScale = Math.Clamp(ZoomScale * zoomFactor, 0.1d, 30d);
        e.Handled = true;
        base.OnPreviewMouseWheel(e);
    }

    public static readonly DependencyProperty DesignWidthProperty =
        DependencyProperty.Register(
            nameof(DesignWidth),
            typeof(double),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutMetricPropertyChanged, CoercePositiveMillimeter));

    public double DesignWidth
    {
        get => (double)GetValue(DesignWidthProperty);
        set => SetValue(DesignWidthProperty, value);
    }

    public static readonly DependencyProperty DesignHeightProperty =
        DependencyProperty.Register(
            nameof(DesignHeight),
            typeof(double),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(60d, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutMetricPropertyChanged, CoercePositiveMillimeter));

    public double DesignHeight
    {
        get => (double)GetValue(DesignHeightProperty);
        set => SetValue(DesignHeightProperty, value);
    }

    public static readonly DependencyProperty ZoomScaleProperty =
        DependencyProperty.Register(
            nameof(ZoomScale),
            typeof(double),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, OnZoomScaleChanged, CoerceZoomScale));

    public double ZoomScale
    {
        get => (double)GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    public static readonly DependencyProperty IsVisibleGridlinesProperty =
        DependencyProperty.Register(
            nameof(IsVisibleGridlines),
            typeof(bool),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public bool IsVisibleGridlines
    {
        get => (bool)GetValue(IsVisibleGridlinesProperty);
        set => SetValue(IsVisibleGridlinesProperty, value);
    }

    public static readonly DependencyProperty GridGapXProperty =
        DependencyProperty.Register(
            nameof(GridGapX),
            typeof(double),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged, CoerceNonNegativeMillimeter));

    public double GridGapX
    {
        get => (double)GetValue(GridGapXProperty);
        set => SetValue(GridGapXProperty, value);
    }

    public static readonly DependencyProperty GridGapYProperty =
        DependencyProperty.Register(
            nameof(GridGapY),
            typeof(double),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged, CoerceNonNegativeMillimeter));

    public double GridGapY
    {
        get => (double)GetValue(GridGapYProperty);
        set => SetValue(GridGapYProperty, value);
    }

    public static readonly DependencyProperty GridOffsetXProperty =
        DependencyProperty.Register(
            nameof(GridOffsetX),
            typeof(double),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public double GridOffsetX
    {
        get => (double)GetValue(GridOffsetXProperty);
        set => SetValue(GridOffsetXProperty, value);
    }

    public static readonly DependencyProperty GridOffsetYProperty =
        DependencyProperty.Register(
            nameof(GridOffsetY),
            typeof(double),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public double GridOffsetY
    {
        get => (double)GetValue(GridOffsetYProperty);
        set => SetValue(GridOffsetYProperty, value);
    }

    public static readonly DependencyProperty GridDotBrushProperty =
        DependencyProperty.Register(
            nameof(GridDotBrush),
            typeof(Brush),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public Brush GridDotBrush
    {
        get => (Brush)GetValue(GridDotBrushProperty);
        set => SetValue(GridDotBrushProperty, value);
    }

    public static readonly DependencyProperty GridDotSizeProperty =
        DependencyProperty.Register(
            nameof(GridDotSize),
            typeof(double),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(0.4d, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged, CoerceNonNegativeMillimeter));

    public double GridDotSize
    {
        get => (double)GetValue(GridDotSizeProperty);
        set => SetValue(GridDotSizeProperty, value);
    }

    public static readonly DependencyProperty PaperBackgroundProperty =
        DependencyProperty.Register(
            nameof(PaperBackground),
            typeof(Brush),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public Brush PaperBackground
    {
        get => (Brush)GetValue(PaperBackgroundProperty);
        set => SetValue(PaperBackgroundProperty, value);
    }

    public static readonly DependencyProperty WorkspaceBackgroundProperty =
        DependencyProperty.Register(
            nameof(WorkspaceBackground),
            typeof(Brush),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(244, 244, 244)), OnVisualPropertyChanged));

    public Brush WorkspaceBackground
    {
        get => (Brush)GetValue(WorkspaceBackgroundProperty);
        set => SetValue(WorkspaceBackgroundProperty, value);
    }

    public static readonly DependencyProperty PaperBorderBrushProperty =
        DependencyProperty.Register(
            nameof(PaperBorderBrush),
            typeof(Brush),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(210, 210, 210)), FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public Brush PaperBorderBrush
    {
        get => (Brush)GetValue(PaperBorderBrushProperty);
        set => SetValue(PaperBorderBrushProperty, value);
    }

    public static readonly DependencyProperty RulerBackgroundProperty =
        DependencyProperty.Register(
            nameof(RulerBackground),
            typeof(Brush),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(248, 248, 248)), OnVisualPropertyChanged));

    public Brush RulerBackground
    {
        get => (Brush)GetValue(RulerBackgroundProperty);
        set => SetValue(RulerBackgroundProperty, value);
    }

    public static readonly DependencyProperty RulerTickBrushProperty =
        DependencyProperty.Register(
            nameof(RulerTickBrush),
            typeof(Brush),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(110, 110, 110)), FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public Brush RulerTickBrush
    {
        get => (Brush)GetValue(RulerTickBrushProperty);
        set => SetValue(RulerTickBrushProperty, value);
    }

    public static readonly DependencyProperty RulerTextBrushProperty =
        DependencyProperty.Register(
            nameof(RulerTextBrush),
            typeof(Brush),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public Brush RulerTextBrush
    {
        get => (Brush)GetValue(RulerTextBrushProperty);
        set => SetValue(RulerTextBrushProperty, value);
    }

    public static readonly DependencyProperty RulerFontSizeProperty =
        DependencyProperty.Register(
            nameof(RulerFontSize),
            typeof(double),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(11d, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged, CoercePositiveNumber));

    public double RulerFontSize
    {
        get => (double)GetValue(RulerFontSizeProperty);
        set => SetValue(RulerFontSizeProperty, value);
    }

    public static readonly DependencyProperty RulerThicknessProperty =
        DependencyProperty.Register(
            nameof(RulerThickness),
            typeof(double),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(28d, FrameworkPropertyMetadataOptions.AffectsMeasure, OnVisualPropertyChanged, CoercePositiveNumber));

    public double RulerThickness
    {
        get => (double)GetValue(RulerThicknessProperty);
        set => SetValue(RulerThicknessProperty, value);
    }

    public static readonly DependencyProperty ScrollBarThicknessProperty =
        DependencyProperty.Register(
            nameof(ScrollBarThickness),
            typeof(double),
            typeof(LdDesignCanvas),
            new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsMeasure, OnVisualPropertyChanged, CoercePositiveNumber));

    public double ScrollBarThickness
    {
        get => (double)GetValue(ScrollBarThicknessProperty);
        set => SetValue(ScrollBarThicknessProperty, value);
    }

    private static void OnLayoutMetricPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LdDesignCanvas control)
        {
            control.UpdateVisualState();
        }
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LdDesignCanvas control)
        {
            control.UpdateVisualState();
        }
    }

    private static void OnZoomScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LdDesignCanvas control)
        {
            return;
        }

        var oldScale = Math.Max((double)e.OldValue, 0.1d);
        var newScale = Math.Max((double)e.NewValue, 0.1d);

        if (Math.Abs(oldScale - newScale) > double.Epsilon)
        {
            control._horizontalOffsetMm = control.ClampOffset(control._horizontalOffsetMm * oldScale / newScale, control._minHorizontalOffsetMm, control._maxHorizontalOffsetMm);
            control._verticalOffsetMm = control.ClampOffset(control._verticalOffsetMm * oldScale / newScale, control._minVerticalOffsetMm, control._maxVerticalOffsetMm);
        }

        control.UpdateVisualState();
    }

    private static object CoerceZoomScale(DependencyObject d, object baseValue) => Math.Clamp((double)baseValue, 0.1d, 30d);

    private static object CoercePositiveMillimeter(DependencyObject d, object baseValue) => Math.Max((double)baseValue, 0.1d);

    private static object CoerceNonNegativeMillimeter(DependencyObject d, object baseValue) => Math.Max((double)baseValue, 0d);

    private static object CoercePositiveNumber(DependencyObject d, object baseValue) => Math.Max((double)baseValue, 1d);

    private void HookTemplateEvents()
    {
        if (_horizontalScrollBar is not null)
        {
            _horizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;
        }

        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.ValueChanged += OnVerticalScrollBarValueChanged;
        }
    }

    private void UnhookTemplateEvents()
    {
        if (_horizontalScrollBar is not null)
        {
            _horizontalScrollBar.ValueChanged -= OnHorizontalScrollBarValueChanged;
        }

        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.ValueChanged -= OnVerticalScrollBarValueChanged;
        }
    }

    private void OnHorizontalScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isApplyingScrollBarValue)
        {
            return;
        }

        _horizontalOffsetMm = ClampOffset(e.NewValue, _minHorizontalOffsetMm, _maxHorizontalOffsetMm);
        ApplySceneTransform();
        UpdateRulers();
    }

    private void OnVerticalScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isApplyingScrollBarValue)
        {
            return;
        }

        _verticalOffsetMm = ClampOffset(e.NewValue, _minVerticalOffsetMm, _maxVerticalOffsetMm);
        ApplySceneTransform();
        UpdateRulers();
    }

    private void UpdateVisualState()
    {
        if (_layoutCanvas is null || _paperCanvas is null)
        {
            return;
        }

        var viewportSize = GetViewportSize();
        var scalePxPerMm = GetScalePxPerMm();

        _lastScalePxPerMm = scalePxPerMm;

        UpdateCanvasMetrics(viewportSize);
        UpdateScrollBars(viewportSize);
        ApplySceneTransform();
        UpdateGridBackground();
        UpdateRulers();
    }

    private void UpdateCanvasMetrics(Size viewportSize)
    {
        if (_layoutCanvas is null || _paperCanvas is null)
        {
            return;
        }

        var viewportWidthMm = ConvertPixelsToMillimeters(viewportSize.Width);
        var viewportHeightMm = ConvertPixelsToMillimeters(viewportSize.Height);
        var workspacePaddingMm = GetWorkspacePaddingMm(viewportWidthMm, viewportHeightMm);

        _layoutCanvas.Width = Math.Max(DesignWidth + workspacePaddingMm, DesignWidth);
        _layoutCanvas.Height = Math.Max(DesignHeight + workspacePaddingMm, DesignHeight);
        _layoutCanvas.Background = WorkspaceBackground;

        _paperCanvas.Width = DesignWidth;
        _paperCanvas.Height = DesignHeight;
        Canvas.SetLeft(_paperCanvas, 0d);
        Canvas.SetTop(_paperCanvas, 0d);
        EnsurePaperBorder();
    }

    private void UpdateScrollBars(Size viewportSize)
    {
        var viewportWidthMm = ConvertPixelsToMillimeters(viewportSize.Width);
        var viewportHeightMm = ConvertPixelsToMillimeters(viewportSize.Height);
        var workspacePaddingMm = GetWorkspacePaddingMm(viewportWidthMm, viewportHeightMm);

        _minHorizontalOffsetMm = -workspacePaddingMm;
        _maxHorizontalOffsetMm = Math.Max(_minHorizontalOffsetMm, DesignWidth + workspacePaddingMm - viewportWidthMm);
        _minVerticalOffsetMm = -workspacePaddingMm;
        _maxVerticalOffsetMm = Math.Max(_minVerticalOffsetMm, DesignHeight + workspacePaddingMm - viewportHeightMm);

        _horizontalOffsetMm = ClampOffset(_horizontalOffsetMm, _minHorizontalOffsetMm, _maxHorizontalOffsetMm);
        _verticalOffsetMm = ClampOffset(_verticalOffsetMm, _minVerticalOffsetMm, _maxVerticalOffsetMm);

        UpdateSingleScrollBar(_horizontalScrollBar, Orientation.Horizontal, _horizontalOffsetMm, _minHorizontalOffsetMm, _maxHorizontalOffsetMm, viewportWidthMm);
        UpdateSingleScrollBar(_verticalScrollBar, Orientation.Vertical, _verticalOffsetMm, _minVerticalOffsetMm, _maxVerticalOffsetMm, viewportHeightMm);
    }

    private void UpdateSingleScrollBar(ScrollBar? scrollBar, Orientation orientation, double value, double minimum, double maximum, double viewportMm)
    {
        if (scrollBar is null)
        {
            return;
        }

        _isApplyingScrollBarValue = true;
        scrollBar.Orientation = orientation;
        scrollBar.Minimum = minimum;
        scrollBar.Maximum = maximum;
        scrollBar.ViewportSize = Math.Max(viewportMm, 0d);
        scrollBar.SmallChange = Math.Max(GetMajorTickStep(_lastScalePxPerMm) / 5d, 0.1d);
        scrollBar.LargeChange = Math.Max(viewportMm * 0.8d, 1d);
        scrollBar.Value = ClampOffset(value, minimum, maximum);
        _isApplyingScrollBarValue = false;
    }

    private void ApplySceneTransform()
    {
        if (_layoutCanvas is null || _paperCanvas is null)
        {
            return;
        }

        var scalePxPerMm = GetScalePxPerMm();
        _layoutCanvas.RenderTransformOrigin = new Point(0d, 0d);
        _layoutCanvas.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(scalePxPerMm, scalePxPerMm),
                new TranslateTransform(-_horizontalOffsetMm * scalePxPerMm, -_verticalOffsetMm * scalePxPerMm),
            }
        };

        _paperCanvas.ClipToBounds = true;
    }

    private void UpdateGridBackground()
    {
        if (_paperCanvas is null)
        {
            return;
        }

        _paperCanvas.Background = CreatePaperBrush();
    }

    private Brush CreatePaperBrush()
    {
        if (!IsVisibleGridlines || GridGapX <= 0d || GridGapY <= 0d || GridDotSize <= 0d)
        {
            return GetPaperBackgroundBrush().CloneCurrentValue();
        }

        var tileWidth = GridGapX;
        var tileHeight = GridGapY;
        var offsetX = NormalizeModulo(GridOffsetX, tileWidth);
        var offsetY = NormalizeModulo(GridOffsetY, tileHeight);
        var radius = Math.Min(GridDotSize / 2d, Math.Min(tileWidth, tileHeight) / 2d);

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(GetPaperBackgroundBrush(), null, new RectangleGeometry(new Rect(0d, 0d, tileWidth, tileHeight))));
        group.Children.Add(new GeometryDrawing(GridDotBrush, null, new EllipseGeometry(new Point(offsetX, offsetY), radius, radius)));

        if (group.CanFreeze)
        {
            group.Freeze();
        }

        var brush = new DrawingBrush(group)
        {
            Stretch = Stretch.None,
            TileMode = TileMode.Tile,
            Viewport = new Rect(0d, 0d, tileWidth, tileHeight),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0d, 0d, tileWidth, tileHeight),
            ViewboxUnits = BrushMappingMode.Absolute,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };

        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }

    private void UpdateRulers()
    {
        var viewportSize = GetViewportSize();
        UpdateTopRuler(viewportSize.Width);
        UpdateLeftRuler(viewportSize.Height);
    }

    private void UpdateTopRuler(double viewportWidth)
    {
        if (_topRuler is null)
        {
            return;
        }

        _topRuler.Children.Clear();
        _topRuler.Width = Math.Max(viewportWidth, 0d);
        _topRuler.Height = RulerThickness;
        _topRuler.ClipToBounds = true;

        var scalePxPerMm = GetScalePxPerMm();
        var visibleWidthMm = ConvertPixelsToMillimeters(viewportWidth);
        var majorStepMm = GetMajorTickStep(scalePxPerMm);
        var firstTick = Math.Floor(_horizontalOffsetMm / majorStepMm) * majorStepMm;
        var tickBottom = Math.Max(RulerThickness - 1d, 0d);
        var tickTop = Math.Max(RulerThickness * 0.4d, 0d);

        for (var tick = firstTick; tick <= _horizontalOffsetMm + visibleWidthMm + majorStepMm; tick += majorStepMm)
        {
            var position = (tick - _horizontalOffsetMm) * scalePxPerMm;
            if (position < 0d || position > viewportWidth)
            {
                continue;
            }

            _topRuler.Children.Add(new Line
            {
                X1 = position,
                Y1 = tickTop,
                X2 = position,
                Y2 = tickBottom,
                Stroke = RulerTickBrush,
                StrokeThickness = 1d,
                SnapsToDevicePixels = true
            });

            var text = CreateRulerLabel(FormatTickValue(tick));
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(text, ClampLabelStart(position + 2d, text.DesiredSize.Width, viewportWidth));
            Canvas.SetTop(text, 2d);
            _topRuler.Children.Add(text);
        }
    }

    private void UpdateLeftRuler(double viewportHeight)
    {
        if (_leftRuler is null)
        {
            return;
        }

        _leftRuler.Children.Clear();
        _leftRuler.Width = RulerThickness;
        _leftRuler.Height = Math.Max(viewportHeight, 0d);
        _leftRuler.ClipToBounds = true;

        var scalePxPerMm = GetScalePxPerMm();
        var visibleHeightMm = ConvertPixelsToMillimeters(viewportHeight);
        var majorStepMm = GetMajorTickStep(scalePxPerMm);
        var firstTick = Math.Floor(_verticalOffsetMm / majorStepMm) * majorStepMm;
        var tickRight = Math.Max(RulerThickness - 1d, 0d);
        var tickLeft = Math.Max(RulerThickness * 0.4d, 0d);

        for (var tick = firstTick; tick <= _verticalOffsetMm + visibleHeightMm + majorStepMm; tick += majorStepMm)
        {
            var position = (tick - _verticalOffsetMm) * scalePxPerMm;
            if (position < 0d || position > viewportHeight)
            {
                continue;
            }

            _leftRuler.Children.Add(new Line
            {
                X1 = tickLeft,
                Y1 = position,
                X2 = tickRight,
                Y2 = position,
                Stroke = RulerTickBrush,
                StrokeThickness = 1d,
                SnapsToDevicePixels = true
            });

            var text = CreateRulerLabel(FormatTickValue(tick));
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(text, Math.Max(1d, RulerThickness - text.DesiredSize.Width - 2d));
            Canvas.SetTop(text, ClampLabelStart(position - text.DesiredSize.Height / 2d, text.DesiredSize.Height, viewportHeight));
            _leftRuler.Children.Add(text);
        }
    }

    private TextBlock CreateRulerLabel(string text) =>
        new()
        {
            Text = text,
            FontSize = RulerFontSize,
            Foreground = RulerTextBrush,
            ClipToBounds = true
        };

    private double GetMajorTickStep(double scalePxPerMm)
    {
        foreach (var candidate in MajorTickCandidates)
        {
            if (candidate * scalePxPerMm >= 40d)
            {
                return candidate;
            }
        }

        return MajorTickCandidates[^1];
    }

    private string FormatTickValue(double value)
    {
        var normalized = Math.Abs(value) < 0.0001d ? 0d : value;
        return normalized.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static double ClampLabelStart(double start, double desiredLength, double boundaryLength)
    {
        var maxStart = Math.Max(boundaryLength - desiredLength - 1d, 0d);
        return Math.Clamp(start, 1d, maxStart);
    }

    private double GetScalePxPerMm()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var dipPerMm = dpi.PixelsPerInchX / 25.4d / dpi.DpiScaleX;
        return dipPerMm * ZoomScale;
    }

    private Size GetViewportSize()
    {
        if (_viewport is null)
        {
            return new Size(0d, 0d);
        }

        return new Size(
            Math.Max(_viewport.ActualWidth, 0d),
            Math.Max(_viewport.ActualHeight, 0d));
    }

    private double ConvertPixelsToMillimeters(double pixels)
    {
        var scalePxPerMm = GetScalePxPerMm();
        return scalePxPerMm <= 0d ? 0d : pixels / scalePxPerMm;
    }

    private double GetWorkspacePaddingMm(double viewportWidthMm, double viewportHeightMm)
    {
        return Math.Max(50d, Math.Max(viewportWidthMm, viewportHeightMm) / 2d);
    }

    private double ClampOffset(double value, double minimum, double maximum)
    {
        if (maximum < minimum)
        {
            return minimum;
        }

        return Math.Clamp(value, minimum, maximum);
    }

    private static double NormalizeModulo(double value, double divisor)
    {
        if (divisor <= 0d)
        {
            return 0d;
        }

        var result = value % divisor;
        return result < 0d ? result + divisor : result;
    }

    private void EnsurePaperBorder()
    {
        if (_paperCanvas is null)
        {
            return;
        }

        _paperBorder ??= new Rectangle
        {
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };

        if (!_paperCanvas.Children.Contains(_paperBorder))
        {
            _paperCanvas.Children.Add(_paperBorder);
        }

        _paperBorder.Width = DesignWidth;
        _paperBorder.Height = DesignHeight;
        _paperBorder.Stroke = PaperBorderBrush;
        _paperBorder.StrokeThickness = 0.2d;
    }

    private Brush GetPaperBackgroundBrush() => PaperBackground ?? Brushes.White;
}
