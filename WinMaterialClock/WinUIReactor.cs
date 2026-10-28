using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using Windows.UI.Text;

namespace WinMaterialClock.Reactor;

/// <summary>
/// WinUIReactor 宣告式視覺節點基底類別，負責將宣告式狀態樹轉換為原生 WinUI 3 <see cref="UIElement"/>。
/// </summary>
public abstract class RxNode
{
    /// <summary>建構並回傳對應的原生 WinUI 3 視覺元素。</summary>
    public abstract UIElement Build();
}

/// <summary>
/// 將原生 WinUI 3 <see cref="UIElement"/> 工廠委派封裝為 WinUIReactor 宣告式節點。
/// </summary>
public sealed class RxElementNode(Func<UIElement> factory) : RxNode
{
    /// <inheritdoc/>
    public override UIElement Build() => factory();
}

/// <summary>
/// WinUIReactor MVU (Model-View-Update) 狀態管理元件基底類別。
/// 透過不可變狀態 <see cref="State"/> 與 <see cref="SetState"/> 驅動單向資料流重新渲染。
/// </summary>
/// <typeparam name="TState">元件狀態資料型別。</typeparam>
public abstract class RxComponent<TState> : RxNode where TState : new()
{
    private readonly Grid _host = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };
    private bool _isMounted;

    /// <summary>取得目前元件的狀態實例。</summary>
    protected TState State { get; private set; } = new();

    /// <summary>當元件首次掛載至視覺樹時觸發之生命週期方法。</summary>
    protected virtual void OnMounted() { }

    /// <summary>當元件即將從視覺樹卸載時觸發之生命週期方法，用於清理事件訂閱與非同步資源。</summary>
    protected virtual void OnWillUnmount() { }

    /// <summary>根據目前 <see cref="State"/> 宣告式建構 UI 視覺樹。</summary>
    public abstract RxNode Render();

    /// <summary>
    /// 以純函式更新元件狀態，並在掛載狀態下自動觸發視覺樹重新渲染。
    /// </summary>
    /// <param name="updater">接收舊狀態並回傳新狀態之轉換函式。</param>
    protected void SetState(Func<TState, TState> updater)
    {
        State = updater(State);
        if (_isMounted)
        {
            InvalidateView();
        }
    }

    /// <summary>強制重新執行 <see cref="Render"/> 並更新宿主內容。</summary>
    protected void InvalidateView()
    {
        _host.Children.Clear();
        _host.Children.Add(Render().Build());
    }

    /// <inheritdoc/>
    public override UIElement Build()
    {
        if (!_isMounted)
        {
            _isMounted = true;
            _host.Loaded += (_, _) => OnMounted();
            _host.Unloaded += (_, _) =>
            {
                if (_isMounted)
                {
                    _isMounted = false;
                    OnWillUnmount();
                }
            };
        }

        _host.Children.Clear();
        _host.Children.Add(Render().Build());
        return _host;
    }
}

/// <summary>
/// 提供 Windows 11 Fluent Design 與 Material You 宣告式 UI 建構輔助函式庫 (DSL)。
/// </summary>
public static class RxUI
{
    /// <summary>建立垂直堆疊容器 (<see cref="StackPanel"/> Orientation=Vertical)。</summary>
    public static RxNode VStack(double spacing, Thickness padding, params ReadOnlySpan<RxNode?> children)
    {
        var list = new List<RxNode>(children.Length);
        foreach (var child in children)
        {
            if (child is not null) list.Add(child);
        }

        return new RxElementNode(() =>
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = spacing,
                Padding = padding
            };
            foreach (var node in list)
                panel.Children.Add(node.Build());
            return panel;
        });
    }

    /// <summary>建立水平堆疊容器 (<see cref="StackPanel"/> Orientation=Horizontal)。</summary>
    public static RxNode HStack(double spacing, params ReadOnlySpan<RxNode?> children)
    {
        var list = new List<RxNode>(children.Length);
        foreach (var child in children)
        {
            if (child is not null) list.Add(child);
        }

        return new RxElementNode(() =>
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = spacing,
                VerticalAlignment = VerticalAlignment.Center
            };
            foreach (var node in list)
                panel.Children.Add(node.Build());
            return panel;
        });
    }

    /// <summary>建立宣告式文字標籤 (<see cref="TextBlock"/>)。</summary>
    public static RxNode Label(
        string text,
        double fontSize,
        ushort fontWeight,
        Color color,
        TextWrapping wrapping = TextWrapping.NoWrap)
    {
        return new RxElementNode(() => new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = new FontWeight(fontWeight),
            Foreground = new SolidColorBrush(color),
            TextWrapping = wrapping,
            VerticalAlignment = VerticalAlignment.Center
        });
    }

    /// <summary>建立遵循 Windows 11 Fluent 規範之圓角 SettingsCard 容器。</summary>
    public static RxNode SettingsCard(
        Color bg,
        Color border,
        double cornerRadius,
        Thickness padding,
        RxNode content)
    {
        return new RxElementNode(() => new Border
        {
            Background = new SolidColorBrush(bg),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(cornerRadius),
            Padding = padding,
            Child = content.Build()
        });
    }

    /// <summary>建立具備預覽圖示徽章、標題說明與右側操作區之標準 Fluent 設定列。</summary>
    public static RxNode SettingsRow(
        RxNode? previewBadge,
        string title,
        string description,
        Color primaryText,
        Color secondaryText,
        RxNode rightAction)
    {
        return new RxElementNode(() =>
        {
            var grid = new Grid { ColumnSpacing = 14 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (previewBadge is not null)
            {
                var badgeEl = previewBadge.Build();
                Grid.SetColumn((FrameworkElement)badgeEl, 0);
                grid.Children.Add(badgeEl);
            }

            var textStack = new StackPanel
            {
                Spacing = 3,
                VerticalAlignment = VerticalAlignment.Center
            };
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(primaryText)
            });
            textStack.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = new SolidColorBrush(secondaryText),
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            var actionEl = rightAction.Build();
            Grid.SetColumn((FrameworkElement)actionEl, 2);
            grid.Children.Add(actionEl);

            return grid;
        });
    }

    /// <summary>建立輕量級 Fluent 互動按鈕（零 XamlControlsResources 模板依賴）。</summary>
    public static RxNode Button(
        string text,
        Color background,
        Color border,
        Color foreground,
        bool isEnabled,
        Action onClick)
    {
        return new RxElementNode(() =>
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(foreground),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var btnBorder = new Border
            {
                Background = new SolidColorBrush(background),
                BorderBrush = new SolidColorBrush(border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(13, 6, 13, 6),
                Opacity = isEnabled ? 1.0 : 0.60,
                Child = label
            };

            if (isEnabled)
            {
                btnBorder.PointerPressed += (_, e) =>
                {
                    e.Handled = true;
                    onClick();
                };
            }

            return btnBorder;
        });
    }

    /// <summary>建立宣告式響應式平滑滑桿列（隨視窗寬度自動伸縮，零控制項模板依賴）。</summary>
    public static RxNode SliderRow(
        string title,
        float min,
        float max,
        float currentValue,
        Color primaryText,
        Color accentColor,
        Color trackBgColor,
        Action<float> onValueChanged,
        Action onCommit)
    {
        return new RxElementNode(() =>
        {
            var rowStack = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(primaryText)
            };
            var valueBlock = new TextBlock
            {
                Text = string.Create(CultureInfo.InvariantCulture, $"{(int)MathF.Round(currentValue * 100f)}%"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accentColor)
            };
            Grid.SetColumn(titleBlock, 0);
            Grid.SetColumn(valueBlock, 1);
            headerGrid.Children.Add(titleBlock);
            headerGrid.Children.Add(valueBlock);
            rowStack.Children.Add(headerGrid);

            const double thumbSize = 16.0;
            var accentBrush = new SolidColorBrush(accentColor);

            var trackContainer = new Grid
            {
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Colors.Transparent)
            };

            var trackHost = new Canvas
            {
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };

            var bgTrack = new Rectangle
            {
                Width = 480,
                Height = 6,
                RadiusX = 3,
                RadiusY = 3,
                Fill = new SolidColorBrush(trackBgColor)
            };
            Canvas.SetTop(bgTrack, 8);
            trackHost.Children.Add(bgTrack);

            double currentNorm = Math.Clamp((currentValue - min) / (max - min), 0.0, 1.0);
            var activeTrack = new Rectangle
            {
                Width = Math.Max(6.0, currentNorm * 480.0),
                Height = 6,
                RadiusX = 3,
                RadiusY = 3,
                Fill = accentBrush
            };
            Canvas.SetTop(activeTrack, 8);
            trackHost.Children.Add(activeTrack);

            var thumb = new Ellipse
            {
                Width = thumbSize,
                Height = thumbSize,
                Fill = accentBrush,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2
            };
            Canvas.SetTop(thumb, 3);
            Canvas.SetLeft(thumb, currentNorm * (480.0 - thumbSize));
            trackHost.Children.Add(thumb);

            void LayoutTrack(double width)
            {
                double w = Math.Max(120.0, width);
                bgTrack.Width = w;
                activeTrack.Width = Math.Max(6.0, currentNorm * w);
                Canvas.SetLeft(thumb, currentNorm * (w - thumbSize));
            }

            trackContainer.SizeChanged += (_, e) => LayoutTrack(e.NewSize.Width);

            bool dragging = false;
            void UpdateAt(double x)
            {
                double w = Math.Max(120.0, trackContainer.ActualWidth);
                currentNorm = Math.Clamp(x / w, 0.0, 1.0);
                LayoutTrack(w);
                float val = min + (float)currentNorm * (max - min);
                valueBlock.Text = string.Create(CultureInfo.InvariantCulture, $"{(int)MathF.Round(val * 100f)}%");
                onValueChanged(val);
            }

            trackContainer.PointerPressed += (_, e) =>
            {
                dragging = true;
                trackContainer.CapturePointer(e.Pointer);
                UpdateAt(e.GetCurrentPoint(trackContainer).Position.X);
            };
            trackContainer.PointerMoved += (_, e) =>
            {
                if (dragging) UpdateAt(e.GetCurrentPoint(trackContainer).Position.X);
            };
            trackContainer.PointerReleased += (_, e) =>
            {
                if (dragging)
                {
                    dragging = false;
                    trackContainer.ReleasePointerCapture(e.Pointer);
                    onCommit();
                }
            };

            trackContainer.Children.Add(trackHost);
            rowStack.Children.Add(trackContainer);
            return rowStack;
        });
    }

    /// <summary>
    /// 建立支援滑鼠滾輪垂直捲動與自適應寬度之垂直堆疊容器（零 ScrollViewer 控制項模板依賴）。
    /// </summary>
    public static RxNode ScrollableVStack(double spacing, Thickness padding, params ReadOnlySpan<RxNode?> children)
    {
        var list = new List<RxNode>(children.Length);
        foreach (var child in children)
        {
            if (child is not null) list.Add(child);
        }

        return new RxElementNode(() =>
        {
            var viewportGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Colors.Transparent)
            };

            var clipGeom = new RectangleGeometry();
            viewportGrid.Clip = clipGeom;

            var translate = new TranslateTransform();
            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = spacing,
                Padding = padding,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = translate
            };

            foreach (var node in list)
                contentPanel.Children.Add(node.Build());

            var scrollThumb = new Border
            {
                Width = 4,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromArgb(95, 160, 160, 175)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 4, 8),
                Visibility = Visibility.Collapsed
            };

            void ClampScroll(double targetY)
            {
                double viewH = viewportGrid.ActualHeight;
                double contentH = contentPanel.ActualHeight;
                double maxScroll = Math.Max(0.0, contentH - viewH);

                if (maxScroll <= 1.0)
                {
                    translate.Y = 0;
                    scrollThumb.Visibility = Visibility.Collapsed;
                    return;
                }

                double clampedY = Math.Clamp(targetY, -maxScroll, 0.0);
                translate.Y = clampedY;

                scrollThumb.Visibility = Visibility.Visible;
                double trackH = Math.Max(40.0, viewH - 16.0);
                double thumbH = Math.Max(28.0, (viewH / contentH) * trackH);
                scrollThumb.Height = thumbH;
                double ratio = -clampedY / maxScroll;
                scrollThumb.Margin = new Thickness(0, 8.0 + ratio * (trackH - thumbH), 4, 0);
            }

            viewportGrid.SizeChanged += (_, e) =>
            {
                clipGeom.Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
                ClampScroll(translate.Y);
            };

            contentPanel.SizeChanged += (_, _) => ClampScroll(translate.Y);

            viewportGrid.PointerWheelChanged += (_, e) =>
            {
                int delta = e.GetCurrentPoint(viewportGrid).Properties.MouseWheelDelta;
                ClampScroll(translate.Y + delta * 0.45);
                e.Handled = true;
            };

            viewportGrid.Children.Add(contentPanel);
            viewportGrid.Children.Add(scrollThumb);
            return viewportGrid;
        });
    }
}
