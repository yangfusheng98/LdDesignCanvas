using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LdDesignCanvas.Controls
{
    /// <summary>
    /// 标尺渲染元素，用于绘制 X/Y 方向的刻度线与数字。
    /// 由 LdDesignCanvas 控件通过模板部件引用并设置参数。
    /// </summary>
    public class RulerElement : FrameworkElement
    {
        #region 依赖属性

        /// <summary>标尺背景画刷</summary>
        public static readonly DependencyProperty BackgroundProperty =
            DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(RulerElement),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush? Background
        {
            get => (Brush?)GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        /// <summary>标尺方向</summary>
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(RulerElement),
                new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsRender));

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        /// <summary>当前可视区域起始值（mm）</summary>
        public static readonly DependencyProperty StartValueProperty =
            DependencyProperty.Register(nameof(StartValue), typeof(double), typeof(RulerElement),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double StartValue
        {
            get => (double)GetValue(StartValueProperty);
            set => SetValue(StartValueProperty, value);
        }

        /// <summary>每毫米对应的显示像素数（已包含缩放）</summary>
        public static readonly DependencyProperty PixelsPerUnitProperty =
            DependencyProperty.Register(nameof(PixelsPerUnit), typeof(double), typeof(RulerElement),
                new FrameworkPropertyMetadata(3.7795, FrameworkPropertyMetadataOptions.AffectsRender));

        public double PixelsPerUnit
        {
            get => (double)GetValue(PixelsPerUnitProperty);
            set => SetValue(PixelsPerUnitProperty, value);
        }

        /// <summary>标尺刻度线颜色</summary>
        public static readonly DependencyProperty TickBrushProperty =
            DependencyProperty.Register(nameof(TickBrush), typeof(Brush), typeof(RulerElement),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush TickBrush
        {
            get => (Brush)GetValue(TickBrushProperty);
            set => SetValue(TickBrushProperty, value);
        }

        /// <summary>标尺文字颜色</summary>
        public static readonly DependencyProperty TextBrushProperty =
            DependencyProperty.Register(nameof(TextBrush), typeof(Brush), typeof(RulerElement),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush TextBrush
        {
            get => (Brush)GetValue(TextBrushProperty);
            set => SetValue(TextBrushProperty, value);
        }

        /// <summary>标尺字体大小</summary>
        public static readonly DependencyProperty RulerFontSizeProperty =
            DependencyProperty.Register(nameof(RulerFontSize), typeof(double), typeof(RulerElement),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double RulerFontSize
        {
            get => (double)GetValue(RulerFontSizeProperty);
            set => SetValue(RulerFontSizeProperty, value);
        }

        #endregion

        // 主刻度候选间隔（mm）
        private static readonly double[] MajorTickIntervals = { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000 };

        // 主刻度之间的最小像素间距
        private const double MinPixelsPerMajorTick = 40.0;

        // 主刻度线长度占标尺厚度的比例
        private const double MajorTickRatio = 0.45;

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

            double tickInterval = CalculateTickInterval();
            double startMm = StartValue;

            bool isHorizontal = (Orientation == Orientation.Horizontal);
            double length = isHorizontal ? width : height;
            double thickness = isHorizontal ? height : width;

            // 计算刻度绘制范围（mm）
            double viewStartMm = startMm;
            double viewEndMm = startMm + length / ppu;

            // 对齐到刻度间隔的起始值
            double firstTick = Math.Floor(viewStartMm / tickInterval) * tickInterval;

            var tickPen = new Pen(TickBrush, 1.0);
            tickPen.Freeze();

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
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
                    typeface,
                    fontSize,
                    TextBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                if (isHorizontal)
                {
                    // 水平标尺：数字绘制在刻度线上方
                    double textX = pixelPos + 2;
                    double textY = 1;

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
                    // 垂直标尺：数字旋转绘制
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
                        dc.PushTransform(new RotateTransform(90, textX + formattedText.Height / 2, textY));
                        dc.DrawText(formattedText, new Point(textX, textY));
                        dc.Pop();
                    }
                    else
                    {
                        dc.DrawText(formattedText, new Point(textX, textY));
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
