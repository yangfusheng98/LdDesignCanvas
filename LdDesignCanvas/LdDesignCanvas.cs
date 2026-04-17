using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LdDesignCanvas;

[TemplatePart(Name = PartTopRuler, Type = typeof(Canvas))]
[TemplatePart(Name = PartLeftRuler, Type = typeof(Canvas))]
[TemplatePart(Name = PartViewportHost, Type = typeof(Border))]
[TemplatePart(Name = PartLayoutCanvas, Type = typeof(Canvas))]
[TemplatePart(Name = PartPaperHost, Type = typeof(Border))]
[TemplatePart(Name = PartGridLayer, Type = typeof(Canvas))]
[TemplatePart(Name = PartPaperCanvas, Type = typeof(Canvas))]
[TemplatePart(Name = PartHScrollBar, Type = typeof(ScrollBar))]
[TemplatePart(Name = PartVScrollBar, Type = typeof(ScrollBar))]
public class LdDesignCanvas : Control
{
    private const string PartTopRuler = "PART_TopRuler";
    private const string PartLeftRuler = "PART_LeftRuler";
    private const string PartViewportHost = "PART_ViewportHost";
    private const string PartLayoutCanvas = "PART_LayoutCanvas";
    private const string PartPaperHost = "PART_PaperHost";
    private const string PartGridLayer = "PART_GridLayer";
    private const string PartPaperCanvas = "PART_PaperCanvas";
    private const string PartHScrollBar = "PART_HScrollBar";
    private const string PartVScrollBar = "PART_VScrollBar";

    private static readonly double[] MajorTickCandidates = [1d, 5d, 10d, 20d, 50d, 100d];
    private const double DipPerInch = 96d;
    private const double MillimetersPerInch = 25.4d;
    private const double MinimumDesignSize = 0.1d;
    private const double MinimumZoom = 0.1d;
    private const double MaximumZoom = 30d;

    private readonly ScaleTransform _layoutScale = new();
    private readonly TranslateTransform _layoutTranslate = new();
    private readonly TransformGroup _layoutTransform = new();

    private Canvas? _topRuler;
    private Canvas? _leftRuler;
    private Border? _viewportHost;
    private Canvas? _layoutCanvas;
    private Border? _paperHost;
    private Canvas? _gridLayer;
    private Canvas? _paperCanvas;
    private ScrollBar? _horizontalScrollBar;
    private ScrollBar? _verticalScrollBar;

    private Rect _workspaceBoundsMm = new(-20d, -20d, 40d, 40d);
    private double _horizontalOffsetMm;
    private double _verticalOffsetMm;
    private double _viewportWidthMm;
    private double _viewportHeightMm;
    private bool _isUpdatingScrollBars;
    private bool _isSyncingDimensions;

    static LdDesignCanvas()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LdDesignCanvas), new FrameworkPropertyMetadata(typeof(LdDesignCanvas)));
    }

    public LdDesignCanvas()
    {
        Focusable = true;
        SnapsToDevicePixels = true;

        _layoutTransform.Children.Add(_layoutScale);
        _layoutTransform.Children.Add(_layoutTranslate);

        SizeChanged += (_, _) => RefreshVisualState();
    }

    public static readonly DependencyProperty DesignWidthProperty = DependencyProperty.Register(
        nameof(DesignWidth),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDesignWidthChanged, CoercePositiveSize));

    public double DesignWidth
    {
        get => (double)GetValue(DesignWidthProperty);
        set => SetValue(DesignWidthProperty, value);
    }

    public static readonly DependencyProperty DesignHeightProperty = DependencyProperty.Register(
        nameof(DesignHeight),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(60d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDesignHeightChanged, CoercePositiveSize));

    public double DesignHeight
    {
        get => (double)GetValue(DesignHeightProperty);
        set => SetValue(DesignHeightProperty, value);
    }

    public static readonly DependencyProperty PaperWidthProperty = DependencyProperty.Register(
        nameof(PaperWidth),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPaperWidthChanged, CoercePositiveSize));

    public double PaperWidth
    {
        get => (double)GetValue(PaperWidthProperty);
        set => SetValue(PaperWidthProperty, value);
    }

    public static readonly DependencyProperty PaperHeightProperty = DependencyProperty.Register(
        nameof(PaperHeight),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(60d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPaperHeightChanged, CoercePositiveSize));

    public double PaperHeight
    {
        get => (double)GetValue(PaperHeightProperty);
        set => SetValue(PaperHeightProperty, value);
    }

    public static readonly DependencyProperty ZoomScaleProperty = DependencyProperty.Register(
        nameof(ZoomScale),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualPropertyChanged, CoerceZoomScale));

    public double ZoomScale
    {
        get => (double)GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    public static readonly DependencyProperty IsVisibleGridlinesProperty = DependencyProperty.Register(
        nameof(IsVisibleGridlines),
        typeof(bool),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(true, OnVisualPropertyChanged));

    public bool IsVisibleGridlines
    {
        get => (bool)GetValue(IsVisibleGridlinesProperty);
        set => SetValue(IsVisibleGridlinesProperty, value);
    }

    public static readonly DependencyProperty GridGapXProperty = DependencyProperty.Register(
        nameof(GridGapX),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(5d, OnVisualPropertyChanged, CoercePositiveSize));

    public double GridGapX
    {
        get => (double)GetValue(GridGapXProperty);
        set => SetValue(GridGapXProperty, value);
    }

    public static readonly DependencyProperty GridGapYProperty = DependencyProperty.Register(
        nameof(GridGapY),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(5d, OnVisualPropertyChanged, CoercePositiveSize));

    public double GridGapY
    {
        get => (double)GetValue(GridGapYProperty);
        set => SetValue(GridGapYProperty, value);
    }

    public static readonly DependencyProperty GridOffsetXProperty = DependencyProperty.Register(
        nameof(GridOffsetX),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(0d, OnVisualPropertyChanged));

    public double GridOffsetX
    {
        get => (double)GetValue(GridOffsetXProperty);
        set => SetValue(GridOffsetXProperty, value);
    }

    public static readonly DependencyProperty GridOffsetYProperty = DependencyProperty.Register(
        nameof(GridOffsetY),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(0d, OnVisualPropertyChanged));

    public double GridOffsetY
    {
        get => (double)GetValue(GridOffsetYProperty);
        set => SetValue(GridOffsetYProperty, value);
    }

    public static readonly DependencyProperty RulerThicknessProperty = DependencyProperty.Register(
        nameof(RulerThickness),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(28d, OnVisualPropertyChanged, CoercePositiveSize));

    public double RulerThickness
    {
        get => (double)GetValue(RulerThicknessProperty);
        set => SetValue(RulerThicknessProperty, value);
    }

    public static readonly DependencyProperty RulerFontSizeProperty = DependencyProperty.Register(
        nameof(RulerFontSize),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(10d, OnVisualPropertyChanged, CoercePositiveSize));

    public double RulerFontSize
    {
        get => (double)GetValue(RulerFontSizeProperty);
        set => SetValue(RulerFontSizeProperty, value);
    }

    public static readonly DependencyProperty RulerBackgroundProperty = DependencyProperty.Register(
        nameof(RulerBackground),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.WhiteSmoke, OnVisualPropertyChanged));

    public Brush RulerBackground
    {
        get => (Brush)GetValue(RulerBackgroundProperty);
        set => SetValue(RulerBackgroundProperty, value);
    }

    public static readonly DependencyProperty RulerTickBrushProperty = DependencyProperty.Register(
        nameof(RulerTickBrush),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.Gray, OnVisualPropertyChanged));

    public Brush RulerTickBrush
    {
        get => (Brush)GetValue(RulerTickBrushProperty);
        set => SetValue(RulerTickBrushProperty, value);
    }

    public static readonly DependencyProperty RulerTextBrushProperty = DependencyProperty.Register(
        nameof(RulerTextBrush),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.Black, OnVisualPropertyChanged));

    public Brush RulerTextBrush
    {
        get => (Brush)GetValue(RulerTextBrushProperty);
        set => SetValue(RulerTextBrushProperty, value);
    }

    public static readonly DependencyProperty RulerBorderBrushProperty = DependencyProperty.Register(
        nameof(RulerBorderBrush),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.LightGray, OnVisualPropertyChanged));

    public Brush RulerBorderBrush
    {
        get => (Brush)GetValue(RulerBorderBrushProperty);
        set => SetValue(RulerBorderBrushProperty, value);
    }

    public static readonly DependencyProperty WorkspaceBackgroundProperty = DependencyProperty.Register(
        nameof(WorkspaceBackground),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.Gainsboro, OnVisualPropertyChanged));

    public Brush WorkspaceBackground
    {
        get => (Brush)GetValue(WorkspaceBackgroundProperty);
        set => SetValue(WorkspaceBackgroundProperty, value);
    }

    public static readonly DependencyProperty PaperBackgroundProperty = DependencyProperty.Register(
        nameof(PaperBackground),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.White, OnVisualPropertyChanged));

    public Brush PaperBackground
    {
        get => (Brush)GetValue(PaperBackgroundProperty);
        set => SetValue(PaperBackgroundProperty, value);
    }

    public static readonly DependencyProperty PaperBorderBrushProperty = DependencyProperty.Register(
        nameof(PaperBorderBrush),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.Silver, OnVisualPropertyChanged));

    public Brush PaperBorderBrush
    {
        get => (Brush)GetValue(PaperBorderBrushProperty);
        set => SetValue(PaperBorderBrushProperty, value);
    }

    public static readonly DependencyProperty GridDotBrushProperty = DependencyProperty.Register(
        nameof(GridDotBrush),
        typeof(Brush),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(Brushes.LightGray, OnVisualPropertyChanged));

    public Brush GridDotBrush
    {
        get => (Brush)GetValue(GridDotBrushProperty);
        set => SetValue(GridDotBrushProperty, value);
    }

    public static readonly DependencyProperty GridDotSizeProperty = DependencyProperty.Register(
        nameof(GridDotSize),
        typeof(double),
        typeof(LdDesignCanvas),
        new FrameworkPropertyMetadata(0.25d, OnVisualPropertyChanged, CoercePositiveSize));

    public double GridDotSize
    {
        get => (double)GetValue(GridDotSizeProperty);
        set => SetValue(GridDotSizeProperty, value);
    }

    public override void OnApplyTemplate()
    {
        UnhookTemplateParts();
        base.OnApplyTemplate();

        _topRuler = GetTemplateChild(PartTopRuler) as Canvas;
        _leftRuler = GetTemplateChild(PartLeftRuler) as Canvas;
        _viewportHost = GetTemplateChild(PartViewportHost) as Border;
        _layoutCanvas = GetTemplateChild(PartLayoutCanvas) as Canvas;
        _paperHost = GetTemplateChild(PartPaperHost) as Border;
        _gridLayer = GetTemplateChild(PartGridLayer) as Canvas;
        _paperCanvas = GetTemplateChild(PartPaperCanvas) as Canvas;
        _horizontalScrollBar = GetTemplateChild(PartHScrollBar) as ScrollBar;
        _verticalScrollBar = GetTemplateChild(PartVScrollBar) as ScrollBar;

        if (_layoutCanvas is not null)
        {
            _layoutCanvas.RenderTransform = _layoutTransform;
            _layoutCanvas.Background = WorkspaceBackground;
        }

        HookTemplateParts();
        RefreshVisualState();
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);

        if (!IsMouseOver || e.Delta == 0)
        {
            return;
        }

        var zoomFactor = e.Delta > 0 ? 1.1d : 1d / 1.1d;
        var newZoom = Math.Clamp(ZoomScale * zoomFactor, MinimumZoom, MaximumZoom);
        if (!DoubleUtil.AreClose(newZoom, ZoomScale))
        {
            ZoomScale = newZoom;
            e.Handled = true;
        }
    }

    private static void OnDesignWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LdDesignCanvas)d;
        control.SyncDimension(PaperWidthProperty, (double)e.NewValue);
        control.RefreshVisualState();
    }

    private static void OnDesignHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LdDesignCanvas)d;
        control.SyncDimension(PaperHeightProperty, (double)e.NewValue);
        control.RefreshVisualState();
    }

    private static void OnPaperWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LdDesignCanvas)d;
        control.SyncDimension(DesignWidthProperty, (double)e.NewValue);
        control.RefreshVisualState();
    }

    private static void OnPaperHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LdDesignCanvas)d;
        control.SyncDimension(DesignHeightProperty, (double)e.NewValue);
        control.RefreshVisualState();
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((LdDesignCanvas)d).RefreshVisualState();
    }

    private static object CoercePositiveSize(DependencyObject d, object baseValue)
    {
        return Math.Max(MinimumDesignSize, (double)baseValue);
    }

    private static object CoerceZoomScale(DependencyObject d, object baseValue)
    {
        return Math.Clamp((double)baseValue, MinimumZoom, MaximumZoom);
    }

    private void SyncDimension(DependencyProperty targetProperty, double value)
    {
        if (_isSyncingDimensions)
        {
            return;
        }

        try
        {
            _isSyncingDimensions = true;
            if (!DoubleUtil.AreClose((double)GetValue(targetProperty), value))
            {
                SetValue(targetProperty, value);
            }
        }
        finally
        {
            _isSyncingDimensions = false;
        }
    }

    private void HookTemplateParts()
    {
        if (_horizontalScrollBar is not null)
        {
            _horizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;
        }

        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.ValueChanged += OnVerticalScrollBarValueChanged;
        }

        if (_viewportHost is not null)
        {
            _viewportHost.SizeChanged += OnViewportSizeChanged;
        }
    }

    private void UnhookTemplateParts()
    {
        if (_horizontalScrollBar is not null)
        {
            _horizontalScrollBar.ValueChanged -= OnHorizontalScrollBarValueChanged;
        }

        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.ValueChanged -= OnVerticalScrollBarValueChanged;
        }

        if (_viewportHost is not null)
        {
            _viewportHost.SizeChanged -= OnViewportSizeChanged;
        }
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshVisualState();
    }

    private void OnHorizontalScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingScrollBars)
        {
            return;
        }

        _horizontalOffsetMm = e.NewValue;
        UpdateLayoutViewport();
        UpdateRulers();
    }

    private void OnVerticalScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingScrollBars)
        {
            return;
        }

        _verticalOffsetMm = e.NewValue;
        UpdateLayoutViewport();
        UpdateRulers();
    }

    private void RefreshVisualState()
    {
        if (_layoutCanvas is null || _viewportHost is null)
        {
            return;
        }

        UpdateViewportMetrics();
        UpdateWorkspaceBounds();
        UpdateScrollBars();
        UpdateLayoutViewport();
        UpdateGridLayer();
        UpdateRulers();
    }

    private void UpdateViewportMetrics()
    {
        var scale = GetDipPerMillimeter();
        if (scale <= 0d)
        {
            _viewportWidthMm = 0d;
            _viewportHeightMm = 0d;
            return;
        }

        _viewportWidthMm = Math.Max(0d, _viewportHost?.ActualWidth ?? 0d) / scale;
        _viewportHeightMm = Math.Max(0d, _viewportHost?.ActualHeight ?? 0d) / scale;
    }

    private void UpdateWorkspaceBounds()
    {
        var marginX = Math.Max(20d, Math.Max(_viewportWidthMm * 0.5d, DesignWidth * 0.25d));
        var marginY = Math.Max(20d, Math.Max(_viewportHeightMm * 0.5d, DesignHeight * 0.25d));

        _workspaceBoundsMm = new Rect(
            -marginX,
            -marginY,
            DesignWidth + (marginX * 2d),
            DesignHeight + (marginY * 2d));
    }

    private void UpdateScrollBars()
    {
        var minX = _workspaceBoundsMm.Left;
        var minY = _workspaceBoundsMm.Top;
        var maxX = Math.Max(minX, _workspaceBoundsMm.Right - _viewportWidthMm);
        var maxY = Math.Max(minY, _workspaceBoundsMm.Bottom - _viewportHeightMm);

        _horizontalOffsetMm = Math.Clamp(_horizontalOffsetMm, minX, maxX);
        _verticalOffsetMm = Math.Clamp(_verticalOffsetMm, minY, maxY);

        _isUpdatingScrollBars = true;
        try
        {
            if (_horizontalScrollBar is not null)
            {
                _horizontalScrollBar.Minimum = minX;
                _horizontalScrollBar.Maximum = maxX;
                _horizontalScrollBar.ViewportSize = Math.Max(0d, _viewportWidthMm);
                _horizontalScrollBar.SmallChange = 1d;
                _horizontalScrollBar.LargeChange = Math.Max(1d, _viewportWidthMm * 0.8d);
                _horizontalScrollBar.Value = _horizontalOffsetMm;
            }

            if (_verticalScrollBar is not null)
            {
                _verticalScrollBar.Minimum = minY;
                _verticalScrollBar.Maximum = maxY;
                _verticalScrollBar.ViewportSize = Math.Max(0d, _viewportHeightMm);
                _verticalScrollBar.SmallChange = 1d;
                _verticalScrollBar.LargeChange = Math.Max(1d, _viewportHeightMm * 0.8d);
                _verticalScrollBar.Value = _verticalOffsetMm;
            }
        }
        finally
        {
            _isUpdatingScrollBars = false;
        }
    }

    private void UpdateLayoutViewport()
    {
        if (_layoutCanvas is null || _paperHost is null || _gridLayer is null || _paperCanvas is null)
        {
            return;
        }

        var dipPerMillimeter = GetDipPerMillimeter();
        _layoutScale.ScaleX = dipPerMillimeter;
        _layoutScale.ScaleY = dipPerMillimeter;
        _layoutTranslate.X = -(_horizontalOffsetMm - _workspaceBoundsMm.Left) * dipPerMillimeter;
        _layoutTranslate.Y = -(_verticalOffsetMm - _workspaceBoundsMm.Top) * dipPerMillimeter;

        _layoutCanvas.Width = _workspaceBoundsMm.Width;
        _layoutCanvas.Height = _workspaceBoundsMm.Height;
        _layoutCanvas.Background = WorkspaceBackground;

        var paperOriginX = -_workspaceBoundsMm.Left;
        var paperOriginY = -_workspaceBoundsMm.Top;

        Canvas.SetLeft(_paperHost, paperOriginX);
        Canvas.SetTop(_paperHost, paperOriginY);
        _paperHost.Width = PaperWidth;
        _paperHost.Height = PaperHeight;
        _paperHost.Background = PaperBackground;
        _paperHost.BorderBrush = PaperBorderBrush;

        _gridLayer.Width = PaperWidth;
        _gridLayer.Height = PaperHeight;

        _paperCanvas.Width = PaperWidth;
        _paperCanvas.Height = PaperHeight;
        _paperCanvas.Background = Brushes.Transparent;
    }

    private void UpdateGridLayer()
    {
        if (_gridLayer is null)
        {
            return;
        }

        if (!IsVisibleGridlines || GridGapX <= 0d || GridGapY <= 0d)
        {
            _gridLayer.Background = null;
            return;
        }

        var dotSize = Math.Min(Math.Min(GridDotSize, GridGapX), GridGapY);
        var offsetX = PositiveModulo(GridOffsetX, GridGapX);
        var offsetY = PositiveModulo(GridOffsetY, GridGapY);

        var drawingGroup = new DrawingGroup();
        drawingGroup.Children.Add(new GeometryDrawing(
            GridDotBrush,
            null,
            new EllipseGeometry(new Point(offsetX, offsetY), dotSize * 0.5d, dotSize * 0.5d)));

        var brush = new DrawingBrush(drawingGroup)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            ViewportUnits = BrushMappingMode.Absolute,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0d, 0d, GridGapX, GridGapY),
            Viewbox = new Rect(0d, 0d, GridGapX, GridGapY)
        };

        brush.Freeze();
        _gridLayer.Background = brush;
    }

    private void UpdateRulers()
    {
        UpdateTopRuler();
        UpdateLeftRuler();
    }

    private void UpdateTopRuler()
    {
        if (_topRuler is null)
        {
            return;
        }

        _topRuler.Children.Clear();
        _topRuler.Background = RulerBackground;

        var width = _topRuler.ActualWidth;
        var height = _topRuler.ActualHeight;
        if (width <= 0d || height <= 0d)
        {
            return;
        }

        _topRuler.Children.Add(new Line
        {
            X1 = 0d,
            X2 = width,
            Y1 = height - 0.5d,
            Y2 = height - 0.5d,
            Stroke = RulerBorderBrush,
            StrokeThickness = 1d,
            SnapsToDevicePixels = true
        });

        var scale = GetDipPerMillimeter();
        var step = GetMajorTickStep(scale);
        var visibleStart = _horizontalOffsetMm;
        var visibleEnd = _horizontalOffsetMm + _viewportWidthMm;
        var startTick = Math.Floor(visibleStart / step) * step;
        var endTick = Math.Ceiling(visibleEnd / step) * step;

        for (var tick = startTick; tick <= endTick + step * 0.5d; tick += step)
        {
            var x = (tick - visibleStart) * scale;
            if (x < -1d || x > width + 1d)
            {
                continue;
            }

            _topRuler.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = height * 0.45d,
                Y2 = height,
                Stroke = RulerTickBrush,
                StrokeThickness = 1d,
                SnapsToDevicePixels = true
            });

            var label = CreateRulerLabel(FormatTickValue(tick));
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var labelWidth = label.DesiredSize.Width;
            var labelX = Math.Clamp(x + 2d, 0d, Math.Max(0d, width - labelWidth - 2d));
            Canvas.SetLeft(label, labelX);
            Canvas.SetTop(label, 2d);
            _topRuler.Children.Add(label);
        }
    }

    private void UpdateLeftRuler()
    {
        if (_leftRuler is null)
        {
            return;
        }

        _leftRuler.Children.Clear();
        _leftRuler.Background = RulerBackground;

        var width = _leftRuler.ActualWidth;
        var height = _leftRuler.ActualHeight;
        if (width <= 0d || height <= 0d)
        {
            return;
        }

        _leftRuler.Children.Add(new Line
        {
            X1 = width - 0.5d,
            X2 = width - 0.5d,
            Y1 = 0d,
            Y2 = height,
            Stroke = RulerBorderBrush,
            StrokeThickness = 1d,
            SnapsToDevicePixels = true
        });

        var scale = GetDipPerMillimeter();
        var step = GetMajorTickStep(scale);
        var visibleStart = _verticalOffsetMm;
        var visibleEnd = _verticalOffsetMm + _viewportHeightMm;
        var startTick = Math.Floor(visibleStart / step) * step;
        var endTick = Math.Ceiling(visibleEnd / step) * step;

        for (var tick = startTick; tick <= endTick + step * 0.5d; tick += step)
        {
            var y = (tick - visibleStart) * scale;
            if (y < -1d || y > height + 1d)
            {
                continue;
            }

            _leftRuler.Children.Add(new Line
            {
                X1 = width * 0.45d,
                X2 = width,
                Y1 = y,
                Y2 = y,
                Stroke = RulerTickBrush,
                StrokeThickness = 1d,
                SnapsToDevicePixels = true
            });

            var label = CreateRulerLabel(FormatTickValue(tick));
            label.TextAlignment = TextAlignment.Right;
            label.Width = Math.Max(0d, width - 4d);
            label.Measure(new Size(label.Width, double.PositiveInfinity));
            var labelHeight = label.DesiredSize.Height;
            var labelY = Math.Clamp(y - (labelHeight * 0.5d), 0d, Math.Max(0d, height - labelHeight - 2d));
            Canvas.SetLeft(label, 0d);
            Canvas.SetTop(label, labelY);
            _leftRuler.Children.Add(label);
        }
    }

    private TextBlock CreateRulerLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = RulerFontSize,
            Foreground = RulerTextBrush,
            Margin = new Thickness(0d),
            Padding = new Thickness(0d),
            TextTrimming = TextTrimming.CharacterEllipsis,
            SnapsToDevicePixels = true
        };
    }

    private static string FormatTickValue(double value)
    {
        var rounded = Math.Round(value, 4);
        return rounded.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static double GetMajorTickStep(double dipPerMillimeter)
    {
        foreach (var candidate in MajorTickCandidates)
        {
            if ((candidate * dipPerMillimeter) >= 36d)
            {
                return candidate;
            }
        }

        return MajorTickCandidates[^1];
    }

    private static double PositiveModulo(double value, double modulo)
    {
        var result = value % modulo;
        return result < 0d ? result + modulo : result;
    }

    private double GetDipPerMillimeter()
    {
        return (DipPerInch / MillimetersPerInch) * ZoomScale;
    }

    private static class DoubleUtil
    {
        private const double Epsilon = 0.0001d;

        public static bool AreClose(double value1, double value2)
        {
            return Math.Abs(value1 - value2) < Epsilon;
        }
    }
}
