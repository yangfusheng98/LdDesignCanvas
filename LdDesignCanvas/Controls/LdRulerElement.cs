using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LdDesignCanvas.Controls
{
    /// <summary>
    /// 标尺渲染元素，用于绘制 X/Y 方向的主刻度线、副刻度线与数字。
    /// 由 LdDesignCanvas 控件通过模板部件引用并设置参数。
    /// </summary>
    public class LdRulerElement : FrameworkElement
    {
        // 缓存 Typeface 对象，避免每次渲染重复创建
        private static readonly Typeface CachedTypeface =
            new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        // 缓存旋转变换对象，在渲染循环中复用
        private readonly RotateTransform _cachedRotateTransform = new();

        #region 依赖属性

        /// <summary>标尺背景画刷</summary>
        public static readonly DependencyProperty BackgroundProperty =
            DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush? Background
        {
            get => (Brush?)GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        /// <summary>标尺方向</summary>
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsRender));

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        /// <summary>当前可视区域起始值（mm）</summary>
        public static readonly DependencyProperty StartValueProperty =
            DependencyProperty.Register(nameof(StartValue), typeof(double), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double StartValue
        {
            get => (double)GetValue(StartValueProperty);
            set => SetValue(StartValueProperty, value);
        }

        /// <summary>每毫米对应的显示像素数（已包含缩放）</summary>
        public static readonly DependencyProperty PixelsPerUnitProperty =
            DependencyProperty.Register(nameof(PixelsPerUnit), typeof(double), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(3.7795, FrameworkPropertyMetadataOptions.AffectsRender));

        public double PixelsPerUnit
        {
            get => (double)GetValue(PixelsPerUnitProperty);
            set => SetValue(PixelsPerUnitProperty, value);
        }

        /// <summary>标尺刻度线颜色</summary>
        public static readonly DependencyProperty TickBrushProperty =
            DependencyProperty.Register(nameof(TickBrush), typeof(Brush), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush TickBrush
        {
            get => (Brush)GetValue(TickBrushProperty);
            set => SetValue(TickBrushProperty, value);
        }

        /// <summary>标尺文字颜色</summary>
        public static readonly DependencyProperty TextBrushProperty =
            DependencyProperty.Register(nameof(TextBrush), typeof(Brush), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush TextBrush
        {
            get => (Brush)GetValue(TextBrushProperty);
            set => SetValue(TextBrushProperty, value);
        }

        /// <summary>标尺字体大小</summary>
        public static readonly DependencyProperty RulerFontSizeProperty =
            DependencyProperty.Register(nameof(RulerFontSize), typeof(double), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double RulerFontSize
        {
            get => (double)GetValue(RulerFontSizeProperty);
            set => SetValue(RulerFontSizeProperty, value);
        }

        /// <summary>高亮区域起始值（mm）</summary>
        public static readonly DependencyProperty HighlightStartProperty =
            DependencyProperty.Register(nameof(HighlightStart), typeof(double), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

        public double HighlightStart
        {
            get => (double)GetValue(HighlightStartProperty);
            set => SetValue(HighlightStartProperty, value);
        }

        /// <summary>高亮区域结束值（mm）</summary>
        public static readonly DependencyProperty HighlightEndProperty =
            DependencyProperty.Register(nameof(HighlightEnd), typeof(double), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

        public double HighlightEnd
        {
            get => (double)GetValue(HighlightEndProperty);
            set => SetValue(HighlightEndProperty, value);
        }

        /// <summary>高亮色画刷</summary>
        public static readonly DependencyProperty HighlightBrushProperty =
            DependencyProperty.Register(nameof(HighlightBrush), typeof(Brush), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0xd3, 0xd1, 0xd3)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush HighlightBrush
        {
            get => (Brush)GetValue(HighlightBrushProperty);
            set => SetValue(HighlightBrushProperty, value);
        }

        /// <summary>
        /// 高亮色沿标尺厚度方向的占比系数（0~1）。
        /// 默认值 1.0，覆盖整个标尺区域（包括刻度线）。
        /// </summary>
        public static readonly DependencyProperty HighlightRatioProperty =
            DependencyProperty.Register(nameof(HighlightRatio), typeof(double), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double HighlightRatio
        {
            get => (double)GetValue(HighlightRatioProperty);
            set => SetValue(HighlightRatioProperty, value);
        }

        /// <summary>光标位置值（mm），NaN 表示不显示光标</summary>
        public static readonly DependencyProperty CursorPositionProperty =
            DependencyProperty.Register(nameof(CursorPosition), typeof(double), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

        public double CursorPosition
        {
            get => (double)GetValue(CursorPositionProperty);
            set => SetValue(CursorPositionProperty, value);
        }

        /// <summary>光标颜色</summary>
        public static readonly DependencyProperty CursorBrushProperty =
            DependencyProperty.Register(nameof(CursorBrush), typeof(Brush), typeof(LdRulerElement),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush CursorBrush
        {
            get => (Brush)GetValue(CursorBrushProperty);
            set => SetValue(CursorBrushProperty, value);
        }

        #endregion

        // 主刻度候选间隔（mm），最大不超过 10mm
        private static readonly double[] MajorTickIntervals = { 0.1, 0.5, 1.0, 5.0, 10.0 };

        // 主刻度之间的最小像素间距
        private const double MinPixelsPerMajorTick = 40.0;

        // 主刻度线长度占标尺厚度的比例
        private const double MajorTickRatio = 0.45;

        // 副刻度线长度占标尺厚度的比例
        private const double MinorTickRatio = 0.25;

        // 每个主刻度之间的副刻度分段数
        private const int MinorTickDivisions = 5;

        /// <summary>
        /// 根据当前缩放比例计算合适的主刻度间隔（mm）。
        /// </summary>
        private double CalculateTickInterval()
        {
            double ppu = PixelsPerUnit;
            if (ppu <= 0) return 10;

            foreach (var interval in MajorTickIntervals)
            {
                if (interval * ppu >= MinPixelsPerMajorTick)
                    return interval;
            }
            return MajorTickIntervals[MajorTickIntervals.Length - 1];
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            double ppu = PixelsPerUnit;
            if (ppu <= 0) return;

            // 绘制背景
            if (Background != null)
                dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));

            bool isHorizontal = (Orientation == Orientation.Horizontal);
            double length = isHorizontal ? width : height;
            double thickness = isHorizontal ? height : width;

            double startMm = StartValue;

            // 绘制高亮区域（Design 宽度/高度对应标尺）
            double hlStart = HighlightStart;
            double hlEnd = HighlightEnd;
            if (!double.IsNaN(hlStart) && !double.IsNaN(hlEnd) && hlEnd > hlStart && HighlightBrush != null)
            {
                double hlPixelStart = (hlStart - startMm) * ppu;
                double hlPixelEnd = (hlEnd - startMm) * ppu;

                // 裁切到可见范围
                hlPixelStart = Math.Max(hlPixelStart, 0);
                hlPixelEnd = Math.Min(hlPixelEnd, length);

                if (hlPixelEnd > hlPixelStart)
                {
                    double hlThickness = thickness * Math.Clamp(HighlightRatio, 0, 1);
                    Rect hlRect;
                    if (isHorizontal)
                        hlRect = new Rect(hlPixelStart, 0, hlPixelEnd - hlPixelStart, hlThickness);
                    else
                        hlRect = new Rect(0, hlPixelStart, hlThickness, hlPixelEnd - hlPixelStart);
                    dc.DrawRectangle(HighlightBrush, null, hlRect);
                }
            }

            double tickInterval = CalculateTickInterval();

            // 计算刻度绘制范围（mm）
            double viewStartMm = startMm;
            double viewEndMm = startMm + length / ppu;

            // 对齐到刻度间隔的起始值
            double firstTick = Math.Floor(viewStartMm / tickInterval) * tickInterval;

            var tickPen = new Pen(TickBrush, 1.0);
            tickPen.Freeze();

            double fontSize = RulerFontSize;

            // 绘制主刻度
            for (double v = firstTick; v <= viewEndMm + tickInterval; v += tickInterval)
            {
                double pixelPos = (v - startMm) * ppu;

                // 超出控件范围则跳过
                if (pixelPos < -1 || pixelPos > length + 1) continue;

                // 绘制刻度线
                double tickLen = thickness * MajorTickRatio;
                if (isHorizontal)
                {
                    dc.DrawLine(tickPen, new Point(pixelPos, thickness - tickLen), new Point(pixelPos, thickness));
                }
                else
                {
                    dc.DrawLine(tickPen, new Point(thickness - tickLen, pixelPos), new Point(thickness, pixelPos));
                }

                // 绘制刻度数字
                string label = FormatTickLabel(v);
                var formattedText = new FormattedText(
                    label,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    CachedTypeface,
                    fontSize,
                    TextBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                if (isHorizontal)
                {
                    // 水平标尺：数字与主刻度顶部平齐，显示在主刻度的右边
                    double textX = pixelPos + 2;
                    double textY = thickness - tickLen;

                    // 裁切检查：确保文字不超出控件右边界
                    if (textX + formattedText.Width > width)
                        continue;
                    // 确保不超出控件左边界
                    if (textX < 0)
                        continue;

                    dc.DrawText(formattedText, new Point(textX, textY));
                }
                else
                {
                    // 垂直标尺：数字与主刻度顶部平齐，显示在主刻度的下方
                    double textX = 1;
                    double textY = pixelPos + 2;

                    // 裁切检查：确保文字不超出控件下边界
                    if (textY + formattedText.Height > height)
                        continue;
                    // 确保不超出控件上边界
                    if (textY < 0)
                        continue;

                    // 垂直标尺上的文字不旋转，直接小号横排显示
                    // 如果文字过宽则使用旋转
                    if (formattedText.Width > thickness - 4)
                    {
                        _cachedRotateTransform.Angle = 90;
                        _cachedRotateTransform.CenterX = textX + formattedText.Height / 2;
                        _cachedRotateTransform.CenterY = textY;
                        dc.PushTransform(_cachedRotateTransform);
                        dc.DrawText(formattedText, new Point(textX, textY));
                        dc.Pop();
                    }
                    else
                    {
                        dc.DrawText(formattedText, new Point(textX, textY));
                    }
                }
            }

            // 绘制副刻度（不显示数字）
            double minorInterval = tickInterval / MinorTickDivisions;
            double minorTickLen = thickness * MinorTickRatio;
            double firstMinorTick = Math.Floor(viewStartMm / minorInterval) * minorInterval;

            for (double v = firstMinorTick; v <= viewEndMm + minorInterval; v += minorInterval)
            {
                // 跳过与主刻度重合的位置
                double remainder = Math.Abs(v % tickInterval);
                if (remainder < minorInterval * 0.1 || remainder > tickInterval - minorInterval * 0.1)
                    continue;

                double pixelPos = (v - startMm) * ppu;

                // 超出控件范围则跳过
                if (pixelPos < -1 || pixelPos > length + 1) continue;

                if (isHorizontal)
                {
                    dc.DrawLine(tickPen, new Point(pixelPos, thickness - minorTickLen), new Point(pixelPos, thickness));
                }
                else
                {
                    dc.DrawLine(tickPen, new Point(thickness - minorTickLen, pixelPos), new Point(thickness, pixelPos));
                }
            }

            // 绘制光标指示线
            double cursorPos = CursorPosition;
            if (!double.IsNaN(cursorPos) && CursorBrush != null)
            {
                double cursorPixel = (cursorPos - startMm) * ppu;
                if (cursorPixel >= 0 && cursorPixel <= length)
                {
                    var cursorPen = new Pen(CursorBrush, 1.0);
                    cursorPen.Freeze();

                    if (isHorizontal)
                    {
                        dc.DrawLine(cursorPen, new Point(cursorPixel, 0), new Point(cursorPixel, thickness));
                    }
                    else
                    {
                        dc.DrawLine(cursorPen, new Point(0, cursorPixel), new Point(thickness, cursorPixel));
                    }
                }
            }
        }

        /// <summary>
        /// 格式化刻度标签文字，去除不必要的小数。
        /// </summary>
        private static string FormatTickLabel(double value)
        {
            // 处理 -0 的情况
            if (Math.Abs(value) < 0.001)
                return "0";

            // 整数值不显示小数
            if (Math.Abs(value - Math.Round(value)) < 0.001)
                return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
