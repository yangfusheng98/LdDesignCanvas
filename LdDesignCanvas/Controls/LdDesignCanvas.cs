using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LdDesignCanvas.Controls
{
    /// <summary>
    /// LdDesignCanvas —— 标签打印设计器核心设计控件。
    /// 采用自定义 Control 方式实现，通过 ControlTemplate 定义外观。
    /// 内部负责标尺、滚动、缩放、网格背景与纸张区域的联动。
    /// </summary>
    [TemplatePart(Name = PART_TopRuler, Type = typeof(LdRulerElement))]
    [TemplatePart(Name = PART_LeftRuler, Type = typeof(LdRulerElement))]
    [TemplatePart(Name = PART_HScrollBar, Type = typeof(ScrollBar))]
    [TemplatePart(Name = PART_VScrollBar, Type = typeof(ScrollBar))]
    [TemplatePart(Name = PART_LayoutCanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_PaperCanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_PaperBorder, Type = typeof(Border))]
    [TemplatePart(Name = PART_LabelBorder, Type = typeof(Border))]
    public class LdDesignCanvas : Control
    {
        #region 模板部件名称常量

        private const string PART_TopRuler = "PART_TopRuler";
        private const string PART_LeftRuler = "PART_LeftRuler";
        private const string PART_HScrollBar = "PART_HScrollBar";
        private const string PART_VScrollBar = "PART_VScrollBar";
        private const string PART_LayoutCanvas = "PART_LayoutCanvas";
        private const string PART_PaperCanvas = "PART_PaperCanvas";
        private const string PART_PaperBorder = "PART_PaperBorder";
        private const string PART_LabelBorder = "PART_LabelBorder";

        #endregion

        #region 模板部件引用

        private LdRulerElement? _topRuler;
        private LdRulerElement? _leftRuler;
        private ScrollBar? _hScrollBar;
        private ScrollBar? _vScrollBar;
        private Canvas? _layoutCanvas;
        private Canvas? _paperCanvas;
        private FrameworkElement? _contentArea;
        private Border? _paperBorder;
        private Border? _labelBorder;

        #endregion

        #region 内部变换对象

        private readonly TranslateTransform _translateTransform = new();
        private readonly ScaleTransform _scaleTransform = new();

        #endregion

        #region 单位转换常量

        // 1 英寸 = 25.4 mm，WPF 默认 96 DPI
        private const double MmPerInch = 25.4;
        private const double WpfDpi = 96.0;

        /// <summary>每毫米对应的 WPF 设备无关像素数（未缩放）</summary>
        private const double BasePixelsPerMm = WpfDpi / MmPerInch;

        #endregion

        #region 依赖属性

        // ========== 设计宽高（mm），同时等于纸张宽高 ==========

        public static readonly DependencyProperty DesignWidthProperty =
            DependencyProperty.Register(nameof(DesignWidth), typeof(double), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(7.62, OnLayoutRelatedPropertyChanged), // 默认: 7.62mm（标准标签宽度）
                ValidatePositiveSize);

        public double DesignWidth
        {
            get => (double)GetValue(DesignWidthProperty);
            set => SetValue(DesignWidthProperty, value);
        }

        public static readonly DependencyProperty DesignHeightProperty =
            DependencyProperty.Register(nameof(DesignHeight), typeof(double), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(5.08, OnLayoutRelatedPropertyChanged), // 默认: 5.08mm（标准标签高度）
                ValidatePositiveSize);

        public double DesignHeight
        {
            get => (double)GetValue(DesignHeightProperty);
            set => SetValue(DesignHeightProperty, value);
        }

        // ========== 缩放比例 ==========

        public static readonly DependencyProperty ZoomScaleProperty =
            DependencyProperty.Register(nameof(ZoomScale), typeof(double), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(1.0, OnZoomScaleChanged, CoerceZoomScale));

        public double ZoomScale
        {
            get => (double)GetValue(ZoomScaleProperty);
            set => SetValue(ZoomScaleProperty, value);
        }

        private static object CoerceZoomScale(DependencyObject d, object baseValue)
        {
            double v = (double)baseValue;
            return Math.Clamp(v, 0.1, 30.0);
        }

        // ========== 网格属性 ==========

        public static readonly DependencyProperty IsVisibleGridlinesProperty =
            DependencyProperty.Register(nameof(IsVisibleGridlines), typeof(bool), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(true, OnGridPropertyChanged));

        public bool IsVisibleGridlines
        {
            get => (bool)GetValue(IsVisibleGridlinesProperty);
            set => SetValue(IsVisibleGridlinesProperty, value);
        }

        public static readonly DependencyProperty GridGapXProperty =
            DependencyProperty.Register(nameof(GridGapX), typeof(double), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(0.1, OnGridPropertyChanged),
                ValidatePositiveSize);

        public double GridGapX
        {
            get => (double)GetValue(GridGapXProperty);
            set => SetValue(GridGapXProperty, value);
        }

        public static readonly DependencyProperty GridGapYProperty =
            DependencyProperty.Register(nameof(GridGapY), typeof(double), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(0.1, OnGridPropertyChanged),
                ValidatePositiveSize);

        public double GridGapY
        {
            get => (double)GetValue(GridGapYProperty);
            set => SetValue(GridGapYProperty, value);
        }

        public static readonly DependencyProperty GridOffsetXProperty =
            DependencyProperty.Register(nameof(GridOffsetX), typeof(double), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(0.0, OnGridPropertyChanged));

        public double GridOffsetX
        {
            get => (double)GetValue(GridOffsetXProperty);
            set => SetValue(GridOffsetXProperty, value);
        }

        public static readonly DependencyProperty GridOffsetYProperty =
            DependencyProperty.Register(nameof(GridOffsetY), typeof(double), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(0.0, OnGridPropertyChanged));

        public double GridOffsetY
        {
            get => (double)GetValue(GridOffsetYProperty);
            set => SetValue(GridOffsetYProperty, value);
        }

        public static readonly DependencyProperty GridPointBrushProperty =
            DependencyProperty.Register(nameof(GridPointBrush), typeof(Brush), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)), OnGridPropertyChanged));

        public Brush GridPointBrush
        {
            get => (Brush)GetValue(GridPointBrushProperty);
            set => SetValue(GridPointBrushProperty, value);
        }

        public static readonly DependencyProperty GridPointSizeProperty =
            DependencyProperty.Register(nameof(GridPointSize), typeof(double), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(0.3, OnGridPropertyChanged),
                ValidatePositiveSize);

        /// <summary>网格点直径（mm）</summary>
        public double GridPointSize
        {
            get => (double)GetValue(GridPointSizeProperty);
            set => SetValue(GridPointSizeProperty, value);
        }

        // ========== 标尺样式属性 ==========

        public static readonly DependencyProperty RulerBackgroundProperty =
            DependencyProperty.Register(nameof(RulerBackground), typeof(Brush), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    OnRulerStylePropertyChanged));

        public Brush RulerBackground
        {
            get => (Brush)GetValue(RulerBackgroundProperty);
            set => SetValue(RulerBackgroundProperty, value);
        }

        public static readonly DependencyProperty RulerForegroundProperty =
            DependencyProperty.Register(nameof(RulerForeground), typeof(Brush), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(Brushes.Black, OnRulerStylePropertyChanged));

        public Brush RulerForeground
        {
            get => (Brush)GetValue(RulerForegroundProperty);
            set => SetValue(RulerForegroundProperty, value);
        }

        public static readonly DependencyProperty RulerTickBrushProperty =
            DependencyProperty.Register(nameof(RulerTickBrush), typeof(Brush), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(Brushes.DarkGray, OnRulerStylePropertyChanged));

        public Brush RulerTickBrush
        {
            get => (Brush)GetValue(RulerTickBrushProperty);
            set => SetValue(RulerTickBrushProperty, value);
        }

        public static readonly DependencyProperty RulerFontSizeProperty =
            DependencyProperty.Register(nameof(RulerFontSize), typeof(double), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(10.0, OnRulerStylePropertyChanged));

        public double RulerFontSize
        {
            get => (double)GetValue(RulerFontSizeProperty);
            set => SetValue(RulerFontSizeProperty, value);
        }

        // ========== 纸张区域背景 ==========

        public static readonly DependencyProperty PaperBackgroundProperty =
            DependencyProperty.Register(nameof(PaperBackground), typeof(Brush), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(Brushes.White, OnPaperBackgroundChanged));

        public Brush PaperBackground
        {
            get => (Brush)GetValue(PaperBackgroundProperty);
            set => SetValue(PaperBackgroundProperty, value);
        }

        // ========== 打印标签圆角 ==========

        public static readonly DependencyProperty LabelCornerRadiusProperty =
            DependencyProperty.Register(nameof(LabelCornerRadius), typeof(CornerRadius), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(new CornerRadius(0), OnLabelCornerRadiusChanged));

        public CornerRadius LabelCornerRadius
        {
            get => (CornerRadius)GetValue(LabelCornerRadiusProperty);
            set => SetValue(LabelCornerRadiusProperty, value);
        }

        // ========== 设计区域背景 ==========

        public static readonly DependencyProperty CanvasBackgroundProperty =
            DependencyProperty.Register(nameof(CanvasBackground), typeof(Brush), typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(200, 200, 200))));

        public Brush CanvasBackground
        {
            get => (Brush)GetValue(CanvasBackgroundProperty);
            set => SetValue(CanvasBackgroundProperty, value);
        }

        #endregion

        #region 验证回调

        private static bool ValidatePositiveSize(object value)
        {
            double v = (double)value;
            return v >= 0 && !double.IsNaN(v) && !double.IsInfinity(v);
        }

        #endregion

        #region 属性变化回调

        private static void OnLayoutRelatedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LdDesignCanvas canvas)
                canvas.UpdateAllLayout();
        }

        private static void OnZoomScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LdDesignCanvas canvas)
            {
                double oldZoom = (double)e.OldValue;
                double newZoom = (double)e.NewValue;
                canvas.OnZoomChanged(oldZoom, newZoom);
            }
        }

        private static void OnGridPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LdDesignCanvas canvas)
                canvas.UpdateGridBackground();
        }

        private static void OnRulerStylePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LdDesignCanvas canvas)
                canvas.UpdateRulerStyles();
        }

        private static void OnPaperBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LdDesignCanvas canvas)
                canvas.UpdatePaperBackground();
        }

        private static void OnLabelCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LdDesignCanvas canvas)
                canvas.UpdateLabelBorder();
        }

        #endregion

        #region 构造函数

        static LdDesignCanvas()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LdDesignCanvas),
                new FrameworkPropertyMetadata(typeof(LdDesignCanvas)));
        }

        #endregion

        #region OnApplyTemplate

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 解绑旧事件
            UnsubscribeEvents();

            // 获取模板部件引用
            _topRuler = GetTemplateChild(PART_TopRuler) as LdRulerElement;
            _leftRuler = GetTemplateChild(PART_LeftRuler) as LdRulerElement;
            _hScrollBar = GetTemplateChild(PART_HScrollBar) as ScrollBar;
            _vScrollBar = GetTemplateChild(PART_VScrollBar) as ScrollBar;
            _layoutCanvas = GetTemplateChild(PART_LayoutCanvas) as Canvas;
            _paperCanvas = GetTemplateChild(PART_PaperCanvas) as Canvas;
            _contentArea = GetTemplateChild("PART_ContentArea") as FrameworkElement;
            _paperBorder = GetTemplateChild(PART_PaperBorder) as Border;
            _labelBorder = GetTemplateChild(PART_LabelBorder) as Border;

            // 绑定事件
            SubscribeEvents();

            // 设置 LayoutCanvas 变换
            SetupLayoutTransform();

            // 初始化布局
            UpdateAllLayout();
        }

        #endregion

        #region 事件订阅与解绑

        private void SubscribeEvents()
        {
            if (_hScrollBar != null)
                _hScrollBar.ValueChanged += OnHScrollBarValueChanged;
            if (_vScrollBar != null)
                _vScrollBar.ValueChanged += OnVScrollBarValueChanged;
            if (_contentArea != null)
                _contentArea.SizeChanged += OnContentAreaSizeChanged;
        }

        private void UnsubscribeEvents()
        {
            if (_hScrollBar != null)
                _hScrollBar.ValueChanged -= OnHScrollBarValueChanged;
            if (_vScrollBar != null)
                _vScrollBar.ValueChanged -= OnVScrollBarValueChanged;
            if (_contentArea != null)
                _contentArea.SizeChanged -= OnContentAreaSizeChanged;
        }

        #endregion

        #region 事件处理

        private void OnHScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateViewTransform();
            UpdateRulers();
        }

        private void OnVScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateViewTransform();
            UpdateRulers();
        }

        private void OnContentAreaSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScrollBars();
            UpdateRulers();
        }

        /// <summary>
        /// 鼠标滚轮事件：Ctrl+滚轮缩放，Shift+滚轮水平滚动，普通滚轮垂直滚动。
        /// </summary>
        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // Ctrl+滚轮 => 缩放
                double factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
                ZoomScale *= factor;
                e.Handled = true;
            }
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                // Shift+滚轮 => 水平滚动
                if (_hScrollBar != null)
                {
                    double scrollStep = GetScrollStep();
                    _hScrollBar.Value -= e.Delta > 0 ? scrollStep : -scrollStep;
                    e.Handled = true;
                }
            }
            else
            {
                // 普通滚轮 => 垂直滚动
                if (_vScrollBar != null)
                {
                    double scrollStep = GetScrollStep();
                    _vScrollBar.Value -= e.Delta > 0 ? scrollStep : -scrollStep;
                    e.Handled = true;
                }
            }
        }

        /// <summary>获取滚轮一次滚动的步进量（mm）</summary>
        private double GetScrollStep()
        {
            // 每次滚动约 10mm，缩放越大步进越小
            return 10.0 / ZoomScale;
        }

        #endregion

        #region 变换设置

        /// <summary>
        /// 为 PART_LayoutCanvas 设置 RenderTransform（平移+缩放）。
        /// </summary>
        private void SetupLayoutTransform()
        {
            if (_layoutCanvas == null) return;

            var group = new TransformGroup();
            // 先平移（mm 域），再缩放到像素
            group.Children.Add(_translateTransform);
            group.Children.Add(_scaleTransform);
            _layoutCanvas.RenderTransform = group;
        }

        #endregion

        #region 核心布局更新

        /// <summary>
        /// 统一更新所有布局元素（纸张尺寸、网格、滚动条、标尺、变换）。
        /// </summary>
        private void UpdateAllLayout()
        {
            UpdatePaperSize();
            UpdateLabelBorder();
            UpdateGridBackground();
            UpdatePaperBackground();
            UpdateScrollBars();
            UpdateViewTransform();
            UpdateRulers();
            UpdateRulerStyles();
        }

        /// <summary>
        /// 更新 PaperCanvas 的逻辑尺寸（mm 值）。
        /// PaperCanvas 的宽高始终等于 DesignWidth / DesignHeight。
        /// 实际显示尺寸由 LayoutCanvas 的变换决定。
        /// </summary>
        private void UpdatePaperSize()
        {
            if (_paperCanvas == null) return;

            _paperCanvas.Width = DesignWidth;
            _paperCanvas.Height = DesignHeight;

            // PaperBorder 锚定在 LayoutCanvas 的 (0,0)
            if (_paperBorder != null)
            {
                Canvas.SetLeft(_paperBorder, 0);
                Canvas.SetTop(_paperBorder, 0);
            }
        }

        /// <summary>
        /// 更新打印标签边框的尺寸和圆角。
        /// </summary>
        private void UpdateLabelBorder()
        {
            if (_labelBorder == null) return;

            _labelBorder.Width = DesignWidth;
            _labelBorder.Height = DesignHeight;
            _labelBorder.CornerRadius = LabelCornerRadius;
            Canvas.SetLeft(_labelBorder, 0);
            Canvas.SetTop(_labelBorder, 0);
        }

        /// <summary>
        /// 更新网格点阵背景。
        /// 网格以 DrawingBrush 点阵形式绘制在 PaperCanvas 的背景上。
        /// </summary>
        private void UpdateGridBackground()
        {
            if (_labelBorder == null) return;

            if (!IsVisibleGridlines || GridGapX <= 0 || GridGapY <= 0)
            {
                // 不显示网格时清除网格层
                ClearGridBackground();
                return;
            }

            double gapX = GridGapX;
            double gapY = GridGapY;
            double offsetX = GridOffsetX;
            double offsetY = GridOffsetY;
            double dotRadius = GridPointSize / 2.0;

            // 在每个 tile 中心绘制一个点
            var dotGeometry = new EllipseGeometry(new Point(gapX / 2.0, gapY / 2.0), dotRadius, dotRadius);
            dotGeometry.Freeze();

            var dotDrawing = new GeometryDrawing(GridPointBrush, null, dotGeometry);
            dotDrawing.Freeze();

            var gridBrush = new DrawingBrush(dotDrawing)
            {
                TileMode = TileMode.Tile,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(offsetX, offsetY, gapX, gapY),
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, gapX, gapY),
                Stretch = Stretch.None
            };
            gridBrush.Freeze();

            // 使用 DrawingGroup 合并纸张背景和网格
            UpdateCombinedPaperBackground(gridBrush);
        }

        /// <summary>
        /// 合并纸张背景色和网格点阵到 LabelBorder 的 Background。
        /// </summary>
        private void UpdateCombinedPaperBackground(DrawingBrush? gridBrush = null)
        {
            if (_labelBorder == null) return;

            if (gridBrush != null && IsVisibleGridlines)
            {
                // 使用 DrawingGroup 叠加背景色和网格点
                var drawingGroup = new DrawingGroup();
                drawingGroup.Children.Add(new GeometryDrawing(
                    PaperBackground, null,
                    new RectangleGeometry(new Rect(0, 0, DesignWidth, DesignHeight))));

                // 网格点层
                var gridRect = new RectangleGeometry(new Rect(0, 0, DesignWidth, DesignHeight));
                gridRect.Freeze();
                drawingGroup.Children.Add(new GeometryDrawing(gridBrush, null, gridRect));
                drawingGroup.Freeze();

                var combinedBrush = new DrawingBrush(drawingGroup)
                {
                    Stretch = Stretch.None,
                    ViewportUnits = BrushMappingMode.Absolute,
                    Viewport = new Rect(0, 0, DesignWidth, DesignHeight),
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(0, 0, DesignWidth, DesignHeight)
                };
                combinedBrush.Freeze();
                _labelBorder.Background = combinedBrush;
            }
            else
            {
                _labelBorder.Background = PaperBackground;
            }
        }

        /// <summary>清除网格背景，仅保留纸张背景色。</summary>
        private void ClearGridBackground()
        {
            if (_labelBorder != null)
                _labelBorder.Background = PaperBackground;
        }

        /// <summary>更新纸张背景色。</summary>
        private void UpdatePaperBackground()
        {
            // 需要重新合并网格和背景
            if (IsVisibleGridlines && GridGapX > 0 && GridGapY > 0)
                UpdateGridBackground();
            else
                ClearGridBackground();
        }

        #endregion

        #region 滚动条更新

        /// <summary>
        /// 更新滚动条的范围与视口大小。
        /// ScrollBar 的值以 mm 为单位。
        /// </summary>
        private void UpdateScrollBars()
        {
            double viewportWidthMm = GetViewportWidthMm();
            double viewportHeightMm = GetViewportHeightMm();

            if (_hScrollBar != null)
            {
                // 水平滚动范围：允许向负方向滚动一个视口宽度，正方向滚动到纸张右侧边缘
                _hScrollBar.Minimum = -viewportWidthMm;
                _hScrollBar.Maximum = DesignWidth;
                _hScrollBar.ViewportSize = viewportWidthMm;
                _hScrollBar.SmallChange = 1.0;
                _hScrollBar.LargeChange = viewportWidthMm * 0.8;
            }

            if (_vScrollBar != null)
            {
                // 垂直滚动范围：同理
                _vScrollBar.Minimum = -viewportHeightMm;
                _vScrollBar.Maximum = DesignHeight;
                _vScrollBar.ViewportSize = viewportHeightMm;
                _vScrollBar.SmallChange = 1.0;
                _vScrollBar.LargeChange = viewportHeightMm * 0.8;
            }
        }

        /// <summary>获取当前视口宽度（mm）</summary>
        private double GetViewportWidthMm()
        {
            double contentWidth = _contentArea?.ActualWidth ?? ActualWidth;
            double factor = BasePixelsPerMm * ZoomScale;
            return factor > 0 ? contentWidth / factor : 0;
        }

        /// <summary>获取当前视口高度（mm）</summary>
        private double GetViewportHeightMm()
        {
            double contentHeight = _contentArea?.ActualHeight ?? ActualHeight;
            double factor = BasePixelsPerMm * ZoomScale;
            return factor > 0 ? contentHeight / factor : 0;
        }

        #endregion

        #region 视图变换更新

        /// <summary>
        /// 更新 LayoutCanvas 的 RenderTransform，同步滚动和缩放。
        /// </summary>
        private void UpdateViewTransform()
        {
            double scrollX = _hScrollBar?.Value ?? 0;
            double scrollY = _vScrollBar?.Value ?? 0;
            double factor = BasePixelsPerMm * ZoomScale;

            _translateTransform.X = -scrollX;
            _translateTransform.Y = -scrollY;
            _scaleTransform.ScaleX = factor;
            _scaleTransform.ScaleY = factor;
        }

        #endregion

        #region 标尺更新

        /// <summary>
        /// 更新标尺的起始值和缩放参数。
        /// </summary>
        private void UpdateRulers()
        {
            double scrollX = _hScrollBar?.Value ?? 0;
            double scrollY = _vScrollBar?.Value ?? 0;
            double factor = BasePixelsPerMm * ZoomScale;

            if (_topRuler != null)
            {
                _topRuler.StartValue = scrollX;
                _topRuler.PixelsPerUnit = factor;
            }

            if (_leftRuler != null)
            {
                _leftRuler.StartValue = scrollY;
                _leftRuler.PixelsPerUnit = factor;
            }
        }

        /// <summary>
        /// 更新标尺的样式属性。
        /// </summary>
        private void UpdateRulerStyles()
        {
            if (_topRuler != null)
            {
                _topRuler.Background = RulerBackground;
                _topRuler.TextBrush = RulerForeground;
                _topRuler.TickBrush = RulerTickBrush;
                _topRuler.RulerFontSize = RulerFontSize;
            }

            if (_leftRuler != null)
            {
                _leftRuler.Background = RulerBackground;
                _leftRuler.TextBrush = RulerForeground;
                _leftRuler.TickBrush = RulerTickBrush;
                _leftRuler.RulerFontSize = RulerFontSize;
            }
        }

        #endregion

        #region 缩放处理

        /// <summary>
        /// 缩放变化回调。
        /// 以 PaperCanvas 中心点为锚点，保持其屏幕位置稳定。
        /// </summary>
        private void OnZoomChanged(double oldZoom, double newZoom)
        {
            if (oldZoom <= 0 || newZoom <= 0) return;

            // 保持纸张中心点在屏幕上的像素位置不变
            // 中心点坐标（mm）: centerX = DesignWidth / 2, centerY = DesignHeight / 2
            // 旧状态下中心点的像素位置: (centerX - scrollX_old) * basePixelsPerMm * oldZoom
            // 新状态下需保持相同: (centerX - scrollX_new) * basePixelsPerMm * newZoom = (centerX - scrollX_old) * basePixelsPerMm * oldZoom
            // => scrollX_new = centerX - (centerX - scrollX_old) * oldZoom / newZoom
            double centerX = DesignWidth / 2.0;
            double centerY = DesignHeight / 2.0;

            if (_hScrollBar != null)
                _hScrollBar.Value = centerX - (centerX - _hScrollBar.Value) * oldZoom / newZoom;
            if (_vScrollBar != null)
                _vScrollBar.Value = centerY - (centerY - _vScrollBar.Value) * oldZoom / newZoom;

            // 更新所有布局
            UpdateScrollBars();
            UpdateViewTransform();
            UpdateRulers();
        }

        #endregion
    }
}
