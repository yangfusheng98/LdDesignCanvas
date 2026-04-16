using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LdDesignCanvas.Controls;

[TemplatePart(Name = PartTopRulerName, Type = typeof(Canvas))]
[TemplatePart(Name = PartLeftRulerName, Type = typeof(Canvas))]
[TemplatePart(Name = PartHorizontalScrollBarName, Type = typeof(ScrollBar))]
[TemplatePart(Name = PartVerticalScrollBarName, Type = typeof(ScrollBar))]
[TemplatePart(Name = PartLayoutCanvasName, Type = typeof(Canvas))]
[TemplatePart(Name = PartPaperCanvasName, Type = typeof(Canvas))]
[TemplatePart(Name = PartPaperSurfaceName, Type = typeof(Border))]
[TemplatePart(Name = PartViewportName, Type = typeof(Border))]
public class LdDesignCanvas : Control
{
    private const double DipPerMillimeter = 96d / 25.4d;
    private const double MinimumLogicalSize = 0.1d;
    private const double MinimumZoom = 0.1d;
    private const double MaximumZoom = 30d;
    private const double MinimumRulerThickness = 20d;
    private const double MinimumFontSize = 6d;
    private const double LabelSafePadding = 2d;
    private const double DefaultGridDotRadius = 0.18d;
    private const double MinimumWorkspaceMarginFallback = 20d;
    private const double TargetMajorTickSpacing = 48d;
    private const double TickEpsilon = 0.0001d;
    private const int MaxRulerTickCount = 4096;
    private static readonly double[] MajorTickCandidates = [1d, 5d, 10d, 20d, 50d, 100d, 200d, 500d, 1000d];

    public const string PartTopRulerName = "PART_TopRuler";
    public const string PartLeftRulerName = "PART_LeftRuler";
    public const string PartHorizontalScrollBarName = "PART_HScrollBar";
    public const string PartVerticalScrollBarName = "PART_VScrollBar";
    public const string PartLayoutCanvasName = "PART_LayoutCanvas";
    public const string PartPaperCanvasName = "PART_PaperCanvas";
    public const string PartPaperSurfaceName = "PART_PaperSurface";
    public const string PartViewportName = "PART_Viewport";

    private readonly TranslateTransform _layoutTranslation = new();
    private readonly ScaleTransform _layoutScale = new();
    private readonly TransformGroup _layoutTransform;

    private Canvas? _topRuler;
    private Canvas? _leftRuler;
    private ScrollBar? _horizontalScrollBar;
    private ScrollBar? _verticalScrollBar;
    private Canvas? _layoutCanvas;
    private Canvas? _paperCanvas;
    private Border? _paperSurface;
    private Border? _viewport;

    private bool _isUpdatingScrollBars;
    private double _horizontalOffset;
    private double _verticalOffset;

    static LdDesignCanvas()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LdDesignCanvas), new FrameworkPropertyMetadata(typeof(LdDesignCanvas)));
    }

    public LdDesignCanvas()
    {
        Focusable = true;
        ClipToBounds = true;

        _layoutTransform = new TransformGroup();
        _layoutTransform.Children.Add(_layoutTranslation);
        _layoutTransform.Children.Add(_layoutScale);

        SizeChanged += (_, _) => RefreshVisualState();
    }

    public static readonly DependencyProperty DesignWidthProperty = DependencyProperty.Register(
        nameof(DesignWidth),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutPropertyChanged, CoerceLogicalSize));

    public static readonly DependencyProperty DesignHeightProperty = DependencyProperty.Register(
        nameof(DesignHeight),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(60d, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutPropertyChanged, CoerceLogicalSize));

    public static readonly DependencyProperty ZoomScaleProperty = DependencyProperty.Register(
        nameof(ZoomScale),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutPropertyChanged, CoerceZoomScale));

    public static readonly DependencyProperty IsVisibleGridlinesProperty = DependencyProperty.Register(
        nameof(IsVisibleGridlines),
        typeof(bool),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(true, OnVisualPropertyChanged));

    public static readonly DependencyProperty GridGapXProperty = DependencyProperty.Register(
        nameof(GridGapX),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(1d, OnVisualPropertyChanged, CoerceLogicalSize));

    public static readonly DependencyProperty GridGapYProperty = DependencyProperty.Register(
        nameof(GridGapY),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(1d, OnVisualPropertyChanged, CoerceLogicalSize));

    public static readonly DependencyProperty GridOffsetXProperty = DependencyProperty.Register(
        nameof(GridOffsetX),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(0d, OnVisualPropertyChanged));

    public static readonly DependencyProperty GridOffsetYProperty = DependencyProperty.Register(
        nameof(GridOffsetY),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(0d, OnVisualPropertyChanged));

    public static readonly DependencyProperty RulerThicknessProperty = DependencyProperty.Register(
        nameof(RulerThickness),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(28d, FrameworkPropertyMetadataOptions.AffectsMeasure, OnVisualPropertyChanged, CoerceRulerThickness));

    public static readonly DependencyProperty RulerFontSizeProperty = DependencyProperty.Register(
        nameof(RulerFontSize),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(10d, OnVisualPropertyChanged, CoerceFontSize));

    public static readonly DependencyProperty WorkspaceMarginProperty = DependencyProperty.Register(
        nameof(WorkspaceMargin),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(40d, OnLayoutPropertyChanged, CoerceNonNegative));

    public static readonly DependencyProperty WorkspaceBackgroundProperty = DependencyProperty.Register(
        nameof(WorkspaceBackground),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty PaperBackgroundProperty = DependencyProperty.Register(
        nameof(PaperBackground),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.White, OnVisualPropertyChanged));

    public static readonly DependencyProperty PaperBorderBrushProperty = DependencyProperty.Register(
        nameof(PaperBorderBrush),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.LightGray));

    public static readonly DependencyProperty PaperBorderThicknessProperty = DependencyProperty.Register(
        nameof(PaperBorderThickness),
        typeof(Thickness),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(new Thickness(1d)));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.Gainsboro, OnVisualPropertyChanged));

    public static readonly DependencyProperty RulerBackgroundProperty = DependencyProperty.Register(
        nameof(RulerBackground),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.WhiteSmoke));

    public static readonly DependencyProperty RulerForegroundProperty = DependencyProperty.Register(
        nameof(RulerForeground),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.DimGray, OnVisualPropertyChanged));

    public static readonly DependencyProperty RulerLineBrushProperty = DependencyProperty.Register(
        nameof(RulerLineBrush),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.Silver, OnVisualPropertyChanged));

    public double DesignWidth
    {
        get => (double)GetValue(DesignWidthProperty);
        set => SetValue(DesignWidthProperty, value);
    }

    public double DesignHeight
    {
        get => (double)GetValue(DesignHeightProperty);
        set => SetValue(DesignHeightProperty, value);
    }

    public double ZoomScale
    {
        get => (double)GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    public bool IsVisibleGridlines
    {
        get => (bool)GetValue(IsVisibleGridlinesProperty);
        set => SetValue(IsVisibleGridlinesProperty, value);
    }

    public double GridGapX
    {
        get => (double)GetValue(GridGapXProperty);
        set => SetValue(GridGapXProperty, value);
    }

    public double GridGapY
    {
        get => (double)GetValue(GridGapYProperty);
        set => SetValue(GridGapYProperty, value);
    }

    public double GridOffsetX
    {
        get => (double)GetValue(GridOffsetXProperty);
        set => SetValue(GridOffsetXProperty, value);
    }

    public double GridOffsetY
    {
        get => (double)GetValue(GridOffsetYProperty);
        set => SetValue(GridOffsetYProperty, value);
    }

    public double RulerThickness
    {
        get => (double)GetValue(RulerThicknessProperty);
        set => SetValue(RulerThicknessProperty, value);
    }

    public double RulerFontSize
    {
        get => (double)GetValue(RulerFontSizeProperty);
        set => SetValue(RulerFontSizeProperty, value);
    }

    public double WorkspaceMargin
    {
        get => (double)GetValue(WorkspaceMarginProperty);
        set => SetValue(WorkspaceMarginProperty, value);
    }

    public Brush WorkspaceBackground
    {
        get => (Brush)GetValue(WorkspaceBackgroundProperty);
        set => SetValue(WorkspaceBackgroundProperty, value);
    }

    public Brush PaperBackground
    {
        get => (Brush)GetValue(PaperBackgroundProperty);
        set => SetValue(PaperBackgroundProperty, value);
    }

    public Brush PaperBorderBrush
    {
        get => (Brush)GetValue(PaperBorderBrushProperty);
        set => SetValue(PaperBorderBrushProperty, value);
    }

    public Thickness PaperBorderThickness
    {
        get => (Thickness)GetValue(PaperBorderThicknessProperty);
        set => SetValue(PaperBorderThicknessProperty, value);
    }

    public Brush GridBrush
    {
        get => (Brush)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public Brush RulerBackground
    {
        get => (Brush)GetValue(RulerBackgroundProperty);
        set => SetValue(RulerBackgroundProperty, value);
    }

    public Brush RulerForeground
    {
        get => (Brush)GetValue(RulerForegroundProperty);
        set => SetValue(RulerForegroundProperty, value);
    }

    public Brush RulerLineBrush
    {
        get => (Brush)GetValue(RulerLineBrushProperty);
        set => SetValue(RulerLineBrushProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        DetachTemplateHandlers();

        _topRuler = GetTemplateChild(PartTopRulerName) as Canvas;
        _leftRuler = GetTemplateChild(PartLeftRulerName) as Canvas;
        _horizontalScrollBar = GetTemplateChild(PartHorizontalScrollBarName) as ScrollBar;
        _verticalScrollBar = GetTemplateChild(PartVerticalScrollBarName) as ScrollBar;
        _layoutCanvas = GetTemplateChild(PartLayoutCanvasName) as Canvas;
        _paperCanvas = GetTemplateChild(PartPaperCanvasName) as Canvas;
        _paperSurface = GetTemplateChild(PartPaperSurfaceName) as Border;
        _viewport = GetTemplateChild(PartViewportName) as Border;

        AttachTemplateHandlers();
        RefreshVisualState();
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if (!IsEnabled)
        {
            base.OnPreviewMouseWheel(e);
            return;
        }

        var factor = e.Delta >= 0 ? 1.1d : 1d / 1.1d;
        ZoomScale = Math.Clamp(ZoomScale * factor, MinimumZoom, MaximumZoom);
        e.Handled = true;
    }

    private void AttachTemplateHandlers()
    {
        if (_horizontalScrollBar is not null)
        {
            _horizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;
        }

        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.ValueChanged += OnVerticalScrollBarValueChanged;
        }

        if (_viewport is not null)
        {
            _viewport.SizeChanged += OnViewportSizeChanged;
        }

        if (_layoutCanvas is not null)
        {
            _layoutCanvas.RenderTransform = _layoutTransform;
            _layoutCanvas.SnapsToDevicePixels = true;
        }

        if (_topRuler is not null)
        {
            _topRuler.ClipToBounds = true;
        }

        if (_leftRuler is not null)
        {
            _leftRuler.ClipToBounds = true;
        }
    }

    private void DetachTemplateHandlers()
    {
        if (_horizontalScrollBar is not null)
        {
            _horizontalScrollBar.ValueChanged -= OnHorizontalScrollBarValueChanged;
        }

        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.ValueChanged -= OnVerticalScrollBarValueChanged;
        }

        if (_viewport is not null)
        {
            _viewport.SizeChanged -= OnViewportSizeChanged;
        }
    }

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        RefreshVisualState();
    }

    private void OnHorizontalScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingScrollBars)
        {
            return;
        }

        _horizontalOffset = e.NewValue;
        UpdateTransforms();
        UpdateRulers();
    }

    private void OnVerticalScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingScrollBars)
        {
            return;
        }

        _verticalOffset = e.NewValue;
        UpdateTransforms();
        UpdateRulers();
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((LdDesignCanvas)d).RefreshVisualState();
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((LdDesignCanvas)d).RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        UpdatePaperMetrics();
        UpdateScrollBars();
        UpdateTransforms();
        UpdatePaperBackground();
        UpdateRulers();
    }

    private void UpdatePaperMetrics()
    {
        if (_paperCanvas is not null)
        {
            _paperCanvas.Width = DesignWidth;
            _paperCanvas.Height = DesignHeight;
            _paperCanvas.Background = Brushes.Transparent;
            Canvas.SetLeft(_paperCanvas, 0d);
            Canvas.SetTop(_paperCanvas, 0d);
            Panel.SetZIndex(_paperCanvas, 1);
        }

        if (_paperSurface is not null)
        {
            _paperSurface.Width = DesignWidth;
            _paperSurface.Height = DesignHeight;
            Canvas.SetLeft(_paperSurface, 0d);
            Canvas.SetTop(_paperSurface, 0d);
            Panel.SetZIndex(_paperSurface, 0);
        }

        if (_layoutCanvas is not null)
        {
            var viewportWidthMm = GetViewportWidthMillimeters();
            var viewportHeightMm = GetViewportHeightMillimeters();
            var workspaceMarginX = GetEffectiveWorkspaceMargin(viewportWidthMm);
            var workspaceMarginY = GetEffectiveWorkspaceMargin(viewportHeightMm);

            _layoutCanvas.Width = Math.Max(DesignWidth + workspaceMarginX, viewportWidthMm);
            _layoutCanvas.Height = Math.Max(DesignHeight + workspaceMarginY, viewportHeightMm);
            _layoutCanvas.Background = Brushes.Transparent;
        }
    }

    private void UpdateScrollBars()
    {
        var viewportWidthMm = GetViewportWidthMillimeters();
        var viewportHeightMm = GetViewportHeightMillimeters();
        var marginX = GetEffectiveWorkspaceMargin(viewportWidthMm);
        var marginY = GetEffectiveWorkspaceMargin(viewportHeightMm);

        var minX = -marginX;
        var minY = -marginY;
        var maxX = Math.Max(minX, DesignWidth + marginX - viewportWidthMm);
        var maxY = Math.Max(minY, DesignHeight + marginY - viewportHeightMm);

        _horizontalOffset = Math.Clamp(_horizontalOffset, minX, maxX);
        _verticalOffset = Math.Clamp(_verticalOffset, minY, maxY);

        _isUpdatingScrollBars = true;

        if (_horizontalScrollBar is not null)
        {
            _horizontalScrollBar.Minimum = minX;
            _horizontalScrollBar.Maximum = maxX;
            _horizontalScrollBar.ViewportSize = viewportWidthMm;
            _horizontalScrollBar.SmallChange = 1d;
            _horizontalScrollBar.LargeChange = Math.Max(1d, viewportWidthMm * 0.8d);
            _horizontalScrollBar.Value = _horizontalOffset;
        }

        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.Minimum = minY;
            _verticalScrollBar.Maximum = maxY;
            _verticalScrollBar.ViewportSize = viewportHeightMm;
            _verticalScrollBar.SmallChange = 1d;
            _verticalScrollBar.LargeChange = Math.Max(1d, viewportHeightMm * 0.8d);
            _verticalScrollBar.Value = _verticalOffset;
        }

        _isUpdatingScrollBars = false;
    }

    private void UpdateTransforms()
    {
        if (_layoutCanvas is null)
        {
            return;
        }

        var scale = GetDipPerMillimeter();
        _layoutTranslation.X = -_horizontalOffset;
        _layoutTranslation.Y = -_verticalOffset;
        _layoutScale.ScaleX = scale;
        _layoutScale.ScaleY = scale;
    }

    private void UpdatePaperBackground()
    {
        var background = CreatePaperBackgroundBrush();

        if (_paperSurface is not null)
        {
            _paperSurface.Background = background;
        }
        else if (_paperCanvas is not null)
        {
            _paperCanvas.Background = background;
        }
    }

    private Brush CreatePaperBackgroundBrush()
    {
        if (!IsVisibleGridlines || GridGapX < MinimumLogicalSize || GridGapY < MinimumLogicalSize)
        {
            return PaperBackground;
        }

        var drawingGroup = new DrawingGroup();
        drawingGroup.Children.Add(new GeometryDrawing(PaperBackground, null, new RectangleGeometry(new Rect(0d, 0d, GridGapX, GridGapY))));
        drawingGroup.Children.Add(new GeometryDrawing(GridBrush, null, new EllipseGeometry(new Point(0d, 0d), DefaultGridDotRadius, DefaultGridDotRadius)));

        var normalizedOffsetX = NormalizeOffset(GridOffsetX, GridGapX);
        var normalizedOffsetY = NormalizeOffset(GridOffsetY, GridGapY);

        var brush = new DrawingBrush(drawingGroup)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            ViewportUnits = BrushMappingMode.Absolute,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0d, 0d, GridGapX, GridGapY),
            Viewport = new Rect(normalizedOffsetX, normalizedOffsetY, GridGapX, GridGapY)
        };

        brush.Freeze();
        return brush;
    }

    private void UpdateRulers()
    {
        UpdateHorizontalRuler();
        UpdateVerticalRuler();
    }

    private void UpdateHorizontalRuler()
    {
        if (_topRuler is null)
        {
            return;
        }

        _topRuler.Children.Clear();

        var width = _topRuler.ActualWidth;
        var height = _topRuler.ActualHeight > 0d ? _topRuler.ActualHeight : RulerThickness;
        if (width <= 0d || height <= 0d)
        {
            return;
        }

        AddBaseline(_topRuler, 0d, height - 1d, width, height - 1d);

        var visibleStart = _horizontalOffset;
        var visibleEnd = visibleStart + GetViewportWidthMillimeters();
        AddMajorTicks(
            _topRuler,
            visibleStart,
            visibleEnd,
            width,
            isHorizontal: true,
            (position, value) => CreateHorizontalTick(position, value, width, height));
    }

    private void UpdateVerticalRuler()
    {
        if (_leftRuler is null)
        {
            return;
        }

        _leftRuler.Children.Clear();

        var width = _leftRuler.ActualWidth > 0d ? _leftRuler.ActualWidth : RulerThickness;
        var height = _leftRuler.ActualHeight;
        if (width <= 0d || height <= 0d)
        {
            return;
        }

        AddBaseline(_leftRuler, width - 1d, 0d, width - 1d, height);

        var visibleStart = _verticalOffset;
        var visibleEnd = visibleStart + GetViewportHeightMillimeters();
        AddMajorTicks(
            _leftRuler,
            visibleStart,
            visibleEnd,
            height,
            isHorizontal: false,
            (position, value) => CreateVerticalTick(position, value, width, height));
    }

    private void AddMajorTicks(Canvas ruler, double visibleStart, double visibleEnd, double rulerSpan, bool isHorizontal, Func<double, double, IEnumerable<UIElement>> elementFactory)
    {
        var tickStep = GetMajorTickInterval();
        var pixelsPerMillimeter = GetDipPerMillimeter();
        var firstTick = Math.Ceiling((visibleStart - TickEpsilon) / tickStep) * tickStep;

        if (double.IsNaN(firstTick) || double.IsInfinity(firstTick))
        {
            firstTick = 0d;
        }

        var tickCount = 0;
        for (var tickValue = firstTick; tickValue <= visibleEnd + TickEpsilon && tickCount < MaxRulerTickCount; tickValue += tickStep, tickCount++)
        {
            var normalizedValue = NormalizeNumber(tickValue);
            var position = (normalizedValue - visibleStart) * pixelsPerMillimeter;
            if (position < -LabelSafePadding || position > rulerSpan + LabelSafePadding)
            {
                continue;
            }

            foreach (var element in elementFactory(position, normalizedValue))
            {
                ruler.Children.Add(element);
            }
        }
    }

    private IEnumerable<UIElement> CreateHorizontalTick(double position, double tickValue, double rulerWidth, double rulerHeight)
    {
        var line = new Line
        {
            X1 = position,
            X2 = position,
            Y1 = Math.Max(0d, rulerHeight - 8d),
            Y2 = rulerHeight,
            Stroke = RulerLineBrush,
            StrokeThickness = 1d,
            SnapsToDevicePixels = true
        };

        var label = CreateRulerLabel(tickValue);
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var left = Math.Clamp(position - (label.DesiredSize.Width / 2d), LabelSafePadding, Math.Max(LabelSafePadding, rulerWidth - label.DesiredSize.Width - LabelSafePadding));
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, LabelSafePadding);

        return [line, label];
    }

    private IEnumerable<UIElement> CreateVerticalTick(double position, double tickValue, double rulerWidth, double rulerHeight)
    {
        var line = new Line
        {
            X1 = Math.Max(0d, rulerWidth - 8d),
            X2 = rulerWidth,
            Y1 = position,
            Y2 = position,
            Stroke = RulerLineBrush,
            StrokeThickness = 1d,
            SnapsToDevicePixels = true
        };

        var label = CreateRulerLabel(tickValue, Math.Max(0d, rulerWidth - (LabelSafePadding * 2d)));
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var top = Math.Clamp(position - (label.DesiredSize.Height / 2d), LabelSafePadding, Math.Max(LabelSafePadding, rulerHeight - label.DesiredSize.Height - LabelSafePadding));
        Canvas.SetLeft(label, LabelSafePadding);
        Canvas.SetTop(label, top);

        return [line, label];
    }

    private TextBlock CreateRulerLabel(double value, double? maxWidth = null)
    {
        var label = new TextBlock
        {
            Text = FormatTickValue(value),
            FontSize = RulerFontSize,
            Foreground = RulerForeground,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        if (maxWidth.HasValue)
        {
            label.Width = maxWidth.Value;
        }

        return label;
    }

    private void AddBaseline(Canvas ruler, double x1, double y1, double x2, double y2)
    {
        ruler.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = RulerLineBrush,
            StrokeThickness = 1d,
            SnapsToDevicePixels = true
        });
    }

    private double GetViewportWidthMillimeters()
    {
        var width = _viewport?.ActualWidth ?? Math.Max(0d, ActualWidth - RulerThickness);
        return width <= 0d ? 0d : width / GetDipPerMillimeter();
    }

    private double GetViewportHeightMillimeters()
    {
        var height = _viewport?.ActualHeight ?? Math.Max(0d, ActualHeight - RulerThickness);
        return height <= 0d ? 0d : height / GetDipPerMillimeter();
    }

    private double GetEffectiveWorkspaceMargin(double viewportSizeMillimeters)
    {
        return Math.Max(WorkspaceMargin, Math.Max(MinimumWorkspaceMarginFallback, viewportSizeMillimeters * 0.5d));
    }

    private double GetDipPerMillimeter()
    {
        return DipPerMillimeter * ZoomScale;
    }

    private double GetMajorTickInterval()
    {
        var pixelsPerMillimeter = GetDipPerMillimeter();

        foreach (var candidate in MajorTickCandidates)
        {
            if (candidate * pixelsPerMillimeter >= TargetMajorTickSpacing)
            {
                return candidate;
            }
        }

        var last = MajorTickCandidates[^1];
        while (last * pixelsPerMillimeter < TargetMajorTickSpacing)
        {
            last *= 2d;
        }

        return last;
    }

    private static double NormalizeOffset(double offset, double gap)
    {
        if (gap <= 0d)
        {
            return 0d;
        }

        var normalized = offset % gap;
        return normalized < 0d ? normalized + gap : normalized;
    }

    private static double NormalizeNumber(double value)
    {
        return Math.Abs(value) < TickEpsilon ? 0d : value;
    }

    private static string FormatTickValue(double value)
    {
        var normalized = NormalizeNumber(value);
        return normalized.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static object CoerceLogicalSize(DependencyObject d, object baseValue)
    {
        return Math.Max(MinimumLogicalSize, (double)baseValue);
    }

    private static object CoerceZoomScale(DependencyObject d, object baseValue)
    {
        return Math.Clamp((double)baseValue, MinimumZoom, MaximumZoom);
    }

    private static object CoerceRulerThickness(DependencyObject d, object baseValue)
    {
        return Math.Max(MinimumRulerThickness, (double)baseValue);
    }

    private static object CoerceFontSize(DependencyObject d, object baseValue)
    {
        return Math.Max(MinimumFontSize, (double)baseValue);
    }

    private static object CoerceNonNegative(DependencyObject d, object baseValue)
    {
        return Math.Max(0d, (double)baseValue);
    }
}
