using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Windows.UI;
using Windows.UI.ViewManagement;
using WinMaterialClock.Reactor;
using WinRT.Interop;

namespace WinMaterialClock;

/// <summary>
/// 定義桌面小工具元件之不可變 MVU 狀態記錄。
/// </summary>
/// <param name="Style">小工具時鐘樣式。</param>
/// <param name="Hour">目前小時 (0-23)。</param>
/// <param name="Minute">目前分鐘 (0-59)。</param>
/// <param name="Second">目前秒數 (0-59)。</param>
/// <param name="IsTopMost">是否處於置頂模式（false 則貼齊桌面底層 HWND_BOTTOM）。</param>
/// <param name="IsClickThrough">是否處於鎖定穿透點擊模式。</param>
public sealed record ClockWidgetState(
    ClockStyle Style,
    int Hour,
    int Minute,
    int Second,
    bool IsTopMost,
    bool IsClickThrough)
{
    public ClockWidgetState() : this(ClockStyle.Pixel2x2, 0, 0, 0, false, false) { }
}

/// <summary>
/// 基於 WinUIReactor 實作之桌面時鐘小工具視圖元件。
/// 內部透過 <see cref="ElementCompositionPreview"/> 掛載純 GPU 硬體加速之 Composition 視覺樹。
/// </summary>
public sealed class ClockWidgetView : RxComponent<ClockWidgetState>
{
    private readonly ClockWidgetWindow _parentWindow;
    private readonly ClockStyle _style;
    private readonly Grid _compositionHost = new()
    {
        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
    };
    private readonly CancellationTokenSource _schedulerCts = new();
    private readonly UISettings _uiSettings = new();

    private WidgetCompositionEngine? _engine;

    public ClockWidgetView(ClockWidgetWindow parentWindow, ClockStyle style)
    {
        _parentWindow = parentWindow;
        _style = style;

        DateTimeOffset now = TimeProvider.System.GetLocalNow();
        SetState(s => s with
        {
            Style = style,
            Hour = now.Hour,
            Minute = now.Minute,
            Second = now.Second,
            IsClickThrough = WidgetManager.IsClickThroughEnabled
        });

        _compositionHost.PointerPressed += OnHostPointerPressed;
        _compositionHost.DoubleTapped += (_, _) => SettingsCenterWindow.ShowInstance();
    }

    /// <summary>元件掛載時初始化 Composition GPU 視覺場景並啟動零輪詢分鐘排程器。</summary>
    protected override void OnMounted()
    {
        Visual hostVisual = ElementCompositionPreview.GetElementVisual(_compositionHost);
        Compositor compositor = hostVisual.Compositor;

        _engine = new WidgetCompositionEngine(compositor, _style, _parentWindow.GetDpiScale());
        _engine.SetScaleAndOpacity(WidgetManager.Settings.Scale, WidgetManager.Settings.Opacity);
        ElementCompositionPreview.SetElementChildVisual(_compositionHost, _engine.RootVisual);

        _engine.ApplyPalette(MaterialYouPalette.GenerateFromSystem(), animate: false);
        SyncClockHandsOrDigits(animate: false);

        _uiSettings.ColorValuesChanged += OnSystemColorsChanged;
        _ = RunMinuteAlignedZeroPollingSchedulerAsync(_schedulerCts.Token);
    }

    /// <summary>元件卸載時取消非同步休眠排程並釋放 Direct2D / Composition 資源。</summary>
    protected override void OnWillUnmount()
    {
        _schedulerCts.Cancel();
        _uiSettings.ColorValuesChanged -= OnSystemColorsChanged;
        _engine?.Dispose();
    }

    /// <summary>
    /// 零輪詢分鐘對齊省電排程器：
    /// 精確計算當前時間距離「下一分鐘 :00 秒」的毫秒差進行非同步休眠，
    /// 其餘 59.9 秒執行緒完全靜止（Analog 秒針已交由 Compositor GPU 無限循環動畫驅動，無須每秒輪詢）。
    /// </summary>
    private async Task RunMinuteAlignedZeroPollingSchedulerAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                DateTimeOffset now = TimeProvider.System.GetLocalNow();
                int delayMs = (60 - now.Second) * 1000 - now.Millisecond + 15;
                if (delayMs <= 0) delayMs = 1000;

                await Task.Delay(delayMs, ct).ConfigureAwait(false);

                _parentWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    DateTimeOffset current = TimeProvider.System.GetLocalNow();
                    SetState(s => s with { Hour = current.Hour, Minute = current.Minute, Second = current.Second });
                    SyncClockHandsOrDigits(animate: true);
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>當系統深淺色或強調色變更時，平滑過渡更新 Material You 配色。</summary>
    public void RefreshThemePalette()
    {
        _engine?.ApplyPalette(MaterialYouPalette.GenerateFromSystem(), animate: true);
    }

    /// <summary>當螢幕 DPI 變更時重新調整字元遮罩解析度。</summary>
    public void UpdateDpi(float dpiScale)
    {
        _engine?.UpdateDpi(dpiScale);
    }

    /// <summary>更新小工具縮放倍率與不透明度。</summary>
    public void UpdateScaleAndOpacity(float scale, float opacity)
    {
        _engine?.SetScaleAndOpacity(scale, opacity);
    }

    /// <summary>同步時間至 GPU Composition 視覺層。</summary>
    public void SyncClockHandsOrDigits(bool animate)
    {
        if (_engine is null) return;
        DateTimeOffset now = TimeProvider.System.GetLocalNow();
        _engine.UpdateTime(now.Hour, now.Minute, now.Second, animate);
    }

    private void OnSystemColorsChanged(UISettings sender, object args)
    {
        _parentWindow.DispatcherQueue.TryEnqueue(RefreshThemePalette);
    }

    private void OnHostPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (WidgetManager.IsClickThroughEnabled)
            return;

        PointerPointProperties props = e.GetCurrentPoint(_compositionHost).Properties;
        if (props.IsLeftButtonPressed)
        {
            e.Handled = true;
            _parentWindow.BeginNativeDrag();
        }
        else if (props.IsRightButtonPressed)
        {
            e.Handled = true;
            _parentWindow.ShowWidgetContextMenu();
        }
    }

    /// <inheritdoc/>
    public override RxNode Render()
    {
        return new RxElementNode(() => _compositionHost);
    }
}

/// <summary>
/// 純 GPU 硬體加速渲染引擎：
/// 負責繪製 Pixel 2x2 數位時鐘、Material You 12 瓣扇形錶盤 (<see cref="ClockStyle.AnalogScalloped"/>) 與經典圓形刻度錶盤 (<see cref="ClockStyle.AnalogClassic"/>)。
/// </summary>
internal sealed partial class WidgetCompositionEngine : IDisposable
{
    public const int WidthDip = 260;
    public const int HeightDip = 300;
    private const int DigitW = 104;
    private const int DigitH = 128;

    private readonly Compositor _compositor;
    private readonly ClockStyle _style;
    private readonly ContainerVisual _rootVisual;

    private readonly CompositionColorBrush _primaryBrush;
    private readonly CompositionColorBrush _secondaryBrush;
    private readonly CompositionColorBrush _tertiaryAccentBrush;
    private readonly CompositionColorBrush _surfaceVariantBrush;
    private readonly CompositionColorBrush _innerSurfaceBrush;
    private readonly CompositionColorBrush _minorTickBrush;

    private readonly SpriteVisual[] _digitSprites = new SpriteVisual[4];
    private readonly CompositionMaskBrush[] _hourMasks = new CompositionMaskBrush[10];
    private readonly CompositionMaskBrush[] _minMasks = new CompositionMaskBrush[10];
    private Direct2DTextAtlas? _textAtlas;
    private float _baseDpiScale;
    private float _userScale = 1.0f;
    private int _lastH1 = -1, _lastH2 = -1, _lastM1 = -1, _lastM2 = -1;

    private ShapeVisual? _hourHandVisual;
    private ShapeVisual? _minuteHandVisual;
    private ShapeVisual? _secondHandVisual;

    public ContainerVisual RootVisual => _rootVisual;

    public WidgetCompositionEngine(Compositor compositor, ClockStyle style, float dpiScale)
    {
        _compositor = compositor;
        _style = style;
        _baseDpiScale = dpiScale;

        _rootVisual = _compositor.CreateContainerVisual();
        _rootVisual.Size = new Vector2(WidthDip, HeightDip);

        _primaryBrush = _compositor.CreateColorBrush(Color.FromArgb(255, 208, 188, 255));
        _secondaryBrush = _compositor.CreateColorBrush(Color.FromArgb(255, 168, 199, 250));
        _tertiaryAccentBrush = _compositor.CreateColorBrush(Color.FromArgb(255, 255, 180, 168));
        _surfaceVariantBrush = _compositor.CreateColorBrush(Color.FromArgb(228, 30, 26, 36));
        _innerSurfaceBrush = _compositor.CreateColorBrush(Color.FromArgb(210, 46, 40, 56));
        _minorTickBrush = _compositor.CreateColorBrush(Color.FromArgb(110, 168, 199, 250));

        if (_style == ClockStyle.Pixel2x2)
        {
            BuildPixel2x2Scene(dpiScale);
        }
        else if (_style == ClockStyle.AnalogScalloped)
        {
            BuildAnalogScallopedScene();
        }
        else
        {
            BuildAnalogClassicScene();
        }
    }

    /// <summary>建構 Pixel 2x2 上下兩列粗體數位時鐘場景。</summary>
    private void BuildPixel2x2Scene(float dpiScale)
    {
        var cardShape = _compositor.CreateShapeVisual();
        cardShape.Size = new Vector2(WidthDip, HeightDip);
        var rectGeom = _compositor.CreateRoundedRectangleGeometry();
        rectGeom.Size = new Vector2(WidthDip, HeightDip);
        rectGeom.CornerRadius = new Vector2(46f, 46f);
        var cardSprite = _compositor.CreateSpriteShape(rectGeom);
        cardSprite.FillBrush = _surfaceVariantBrush;
        cardShape.Shapes.Add(cardSprite);
        _rootVisual.Children.InsertAtBottom(cardShape);

        ReadOnlySpan<Vector3> offsets =
        [
            new Vector3(26f, 16f, 0f),
            new Vector3(130f, 16f, 0f),
            new Vector3(26f, 152f, 0f),
            new Vector3(130f, 152f, 0f)
        ];

        for (int i = 0; i < 4; i++)
        {
            var sprite = _compositor.CreateSpriteVisual();
            sprite.Size = new Vector2(DigitW, DigitH);
            sprite.Offset = offsets[i];
            _digitSprites[i] = sprite;
            _rootVisual.Children.InsertAtTop(sprite);
        }

        _textAtlas = new Direct2DTextAtlas(_compositor, DigitW, DigitH, dpiScale);
        RebuildDigitMaskBrushes();
    }

    private void RebuildDigitMaskBrushes()
    {
        if (_textAtlas is null) return;
        for (int i = 0; i <= 9; i++)
        {
            var mask = _textAtlas.GetDigitBrush(i);
            var hBrush = _compositor.CreateMaskBrush();
            hBrush.Mask = mask;
            hBrush.Source = _primaryBrush;
            _hourMasks[i] = hBrush;

            var mBrush = _compositor.CreateMaskBrush();
            mBrush.Mask = mask;
            mBrush.Source = _secondaryBrush;
            _minMasks[i] = mBrush;
        }
    }

    /// <summary>
    /// 建構 Google Pixel 經典 Material You 12 瓣扇形波浪錶盤 (<see cref="ClockStyle.AnalogScalloped"/>)：
    /// 透過中心圓盤與 12 個環繞花瓣圓弧組合成標誌性的 Pixel 扇形幾何色塊。
    /// </summary>
    private void BuildAnalogScallopedScene()
    {
        var dialVisual = _compositor.CreateShapeVisual();
        dialVisual.Size = new Vector2(WidthDip, HeightDip);
        var center = new Vector2(130f, 150f);

        const int lobes = 12;
        const float ringRadius = 86f;
        const float lobeRadius = 35f;

        for (int i = 0; i < lobes; i++)
        {
            float angleRad = (i * (360f / lobes)) * (MathF.PI / 180f);
            var lobeGeom = _compositor.CreateEllipseGeometry();
            lobeGeom.Center = new Vector2(
                center.X + MathF.Cos(angleRad) * ringRadius,
                center.Y + MathF.Sin(angleRad) * ringRadius);
            lobeGeom.Radius = new Vector2(lobeRadius, lobeRadius);

            var lobeShape = _compositor.CreateSpriteShape(lobeGeom);
            lobeShape.FillBrush = _surfaceVariantBrush;
            dialVisual.Shapes.Add(lobeShape);
        }

        var coreGeom = _compositor.CreateEllipseGeometry();
        coreGeom.Center = center;
        coreGeom.Radius = new Vector2(98f, 98f);
        var coreShape = _compositor.CreateSpriteShape(coreGeom);
        coreShape.FillBrush = _surfaceVariantBrush;
        dialVisual.Shapes.Add(coreShape);

        for (int i = 0; i < 12; i++)
        {
            float rad = (i * 30f - 90f) * (MathF.PI / 180f);
            bool isCardinal = (i % 3) == 0;
            var dotGeom = _compositor.CreateEllipseGeometry();
            dotGeom.Center = new Vector2(center.X + MathF.Cos(rad) * 94f, center.Y + MathF.Sin(rad) * 94f);
            float r = isCardinal ? 5.5f : 3.0f;
            dotGeom.Radius = new Vector2(r, r);

            var dotShape = _compositor.CreateSpriteShape(dotGeom);
            dotShape.FillBrush = isCardinal ? _primaryBrush : _minorTickBrush;
            dialVisual.Shapes.Add(dotShape);
        }

        _rootVisual.Children.InsertAtTop(dialVisual);

        _hourHandVisual = CreatePillHand(width: 16f, length: 64f, tail: 6f, _primaryBrush);
        _minuteHandVisual = CreatePillHand(width: 11f, length: 90f, tail: 8f, _secondaryBrush);
        _secondHandVisual = CreateSecondHandWithCap();

        _rootVisual.Children.InsertAtTop(_hourHandVisual);
        _rootVisual.Children.InsertAtTop(_minuteHandVisual);
        _rootVisual.Children.InsertAtTop(_secondHandVisual);
    }

    /// <summary>建構經典圓形 60 刻度指針錶盤 (<see cref="ClockStyle.AnalogClassic"/>)。</summary>
    private void BuildAnalogClassicScene()
    {
        var dialVisual = _compositor.CreateShapeVisual();
        dialVisual.Size = new Vector2(WidthDip, HeightDip);
        var center = new Vector2(130f, 150f);

        var outerCircle = _compositor.CreateEllipseGeometry();
        outerCircle.Center = center;
        outerCircle.Radius = new Vector2(122f, 122f);
        var outerShape = _compositor.CreateSpriteShape(outerCircle);
        outerShape.FillBrush = _surfaceVariantBrush;
        dialVisual.Shapes.Add(outerShape);

        var innerCircle = _compositor.CreateEllipseGeometry();
        innerCircle.Center = center;
        innerCircle.Radius = new Vector2(96f, 96f);
        var innerShape = _compositor.CreateSpriteShape(innerCircle);
        innerShape.FillBrush = _innerSurfaceBrush;
        dialVisual.Shapes.Add(innerShape);

        for (int i = 0; i < 60; i++)
        {
            float rad = (i * 6f - 90f) * (MathF.PI / 180f);
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);
            bool isCardinal = (i % 15) == 0;
            bool isHour = (i % 5) == 0;

            float rOuter = 114f;
            float rInner = isCardinal ? 96f : (isHour ? 101f : 107f);
            var line = _compositor.CreateLineGeometry();
            line.Start = new Vector2(center.X + cos * rInner, center.Y + sin * rInner);
            line.End = new Vector2(center.X + cos * rOuter, center.Y + sin * rOuter);

            var tick = _compositor.CreateSpriteShape(line);
            tick.StrokeThickness = isCardinal ? 4.2f : (isHour ? 2.6f : 1.3f);
            tick.StrokeStartCap = CompositionStrokeCap.Round;
            tick.StrokeEndCap = CompositionStrokeCap.Round;
            tick.StrokeBrush = isCardinal ? _primaryBrush : (isHour ? _secondaryBrush : _minorTickBrush);
            dialVisual.Shapes.Add(tick);
        }

        _rootVisual.Children.InsertAtTop(dialVisual);

        _hourHandVisual = CreatePillHand(width: 11f, length: 68f, tail: 8f, _primaryBrush);
        _minuteHandVisual = CreatePillHand(width: 7.5f, length: 94f, tail: 10f, _secondaryBrush);
        _secondHandVisual = CreateSecondHandWithCap();

        _rootVisual.Children.InsertAtTop(_hourHandVisual);
        _rootVisual.Children.InsertAtTop(_minuteHandVisual);
        _rootVisual.Children.InsertAtTop(_secondHandVisual);
    }

    private ShapeVisual CreatePillHand(float width, float length, float tail, CompositionBrush brush)
    {
        var sv = _compositor.CreateShapeVisual();
        sv.Size = new Vector2(WidthDip, HeightDip);
        sv.CenterPoint = new Vector3(130f, 150f, 0f);

        var geom = _compositor.CreateRoundedRectangleGeometry();
        geom.Size = new Vector2(width, length);
        geom.Offset = new Vector2(130f - width * 0.5f, 150f - (length - tail));
        geom.CornerRadius = new Vector2(width * 0.5f, width * 0.5f);

        var shape = _compositor.CreateSpriteShape(geom);
        shape.FillBrush = brush;
        sv.Shapes.Add(shape);
        return sv;
    }

    private ShapeVisual CreateSecondHandWithCap()
    {
        var sv = _compositor.CreateShapeVisual();
        sv.Size = new Vector2(WidthDip, HeightDip);
        sv.CenterPoint = new Vector3(130f, 150f, 0f);

        var needle = _compositor.CreateRoundedRectangleGeometry();
        needle.Size = new Vector2(2.8f, 116f);
        needle.Offset = new Vector2(130f - 1.4f, 150f - 98f);
        needle.CornerRadius = new Vector2(1.4f, 1.4f);
        var needleShape = _compositor.CreateSpriteShape(needle);
        needleShape.FillBrush = _tertiaryAccentBrush;
        sv.Shapes.Add(needleShape);

        var capOuter = _compositor.CreateEllipseGeometry();
        capOuter.Center = new Vector2(130f, 150f);
        capOuter.Radius = new Vector2(7.5f, 7.5f);
        var capOuterShape = _compositor.CreateSpriteShape(capOuter);
        capOuterShape.FillBrush = _tertiaryAccentBrush;
        sv.Shapes.Add(capOuterShape);

        var capInner = _compositor.CreateEllipseGeometry();
        capInner.Center = new Vector2(130f, 150f);
        capInner.Radius = new Vector2(3.2f, 3.2f);
        var capInnerShape = _compositor.CreateSpriteShape(capInner);
        capInnerShape.FillBrush = _surfaceVariantBrush;
        sv.Shapes.Add(capInnerShape);

        return sv;
    }

    /// <summary>
    /// 更新時鐘時間：
    /// - Pixel2x2：僅對發生變化的位數觸發 300ms Material 淡入與微位移 KeyFrameAnimation。
    /// - Analog：設定時針與分針角度，並將秒針交由 Compositor 的 ScalarKeyFrameAnimation 無限循環執行，維持 0% CPU 開銷。
    /// </summary>
    public void UpdateTime(int hour, int minute, int second, bool animate)
    {
        if (_style == ClockStyle.Pixel2x2)
        {
            UpdateSingleDigit(0, hour / 10, ref _lastH1, _hourMasks[hour / 10], 16f, animate);
            UpdateSingleDigit(1, hour % 10, ref _lastH2, _hourMasks[hour % 10], 16f, animate);
            UpdateSingleDigit(2, minute / 10, ref _lastM1, _minMasks[minute / 10], 152f, animate);
            UpdateSingleDigit(3, minute % 10, ref _lastM2, _minMasks[minute % 10], 152f, animate);
        }
        else if (_hourHandVisual is not null && _minuteHandVisual is not null && _secondHandVisual is not null)
        {
            float hourAngle = (hour % 12) * 30f + minute * 0.5f + second * (0.5f / 60f);
            float minuteAngle = minute * 6f + second * 0.1f;
            float secondStartAngle = second * 6f;

            _hourHandVisual.RotationAngleInDegrees = hourAngle;
            _minuteHandVisual.RotationAngleInDegrees = minuteAngle;

            var linear = _compositor.CreateLinearEasingFunction();
            var secLoopAnim = _compositor.CreateScalarKeyFrameAnimation();
            secLoopAnim.InsertKeyFrame(0f, secondStartAngle, linear);
            secLoopAnim.InsertKeyFrame(1f, secondStartAngle + 360f, linear);
            secLoopAnim.Duration = TimeSpan.FromSeconds(60);
            secLoopAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            _secondHandVisual.StartAnimation(nameof(Visual.RotationAngleInDegrees), secLoopAnim);
        }
    }

    private void UpdateSingleDigit(
        int idx, int newDigit, ref int lastDigit,
        CompositionMaskBrush brush, float baseY, bool animate)
    {
        if (lastDigit == newDigit) return;
        lastDigit = newDigit;

        SpriteVisual sprite = _digitSprites[idx];
        sprite.Brush = brush;

        if (!animate) return;

        var easing = _compositor.CreateCubicBezierEasingFunction(new Vector2(0.05f, 0.7f), new Vector2(0.1f, 1.0f));
        var duration = TimeSpan.FromMilliseconds(300);

        var opAnim = _compositor.CreateScalarKeyFrameAnimation();
        opAnim.InsertKeyFrame(0f, 0.15f);
        opAnim.InsertKeyFrame(1f, 1.0f, easing);
        opAnim.Duration = duration;

        float x = sprite.Offset.X;
        var offAnim = _compositor.CreateVector3KeyFrameAnimation();
        offAnim.InsertKeyFrame(0f, new Vector3(x, baseY + 12f, 0f));
        offAnim.InsertKeyFrame(1f, new Vector3(x, baseY, 0f), easing);
        offAnim.Duration = duration;

        sprite.StartAnimation(nameof(Visual.Opacity), opAnim);
        sprite.StartAnimation(nameof(Visual.Offset), offAnim);
    }

    public void ApplyPalette(MaterialPalette palette, bool animate)
    {
        Color minorTick = Color.FromArgb(105, palette.MinuteDigits.R, palette.MinuteDigits.G, palette.MinuteDigits.B);
        if (!animate)
        {
            _primaryBrush.Color = palette.HourDigits;
            _secondaryBrush.Color = palette.MinuteDigits;
            _tertiaryAccentBrush.Color = palette.SecondHandAccent;
            _surfaceVariantBrush.Color = palette.CardSurface;
            _innerSurfaceBrush.Color = palette.FluentInnerSurface;
            _minorTickBrush.Color = minorTick;
            return;
        }

        var d = TimeSpan.FromMilliseconds(400);
        AnimateColor(_primaryBrush, palette.HourDigits, d);
        AnimateColor(_secondaryBrush, palette.MinuteDigits, d);
        AnimateColor(_tertiaryAccentBrush, palette.SecondHandAccent, d);
        AnimateColor(_surfaceVariantBrush, palette.CardSurface, d);
        AnimateColor(_innerSurfaceBrush, palette.FluentInnerSurface, d);
        AnimateColor(_minorTickBrush, minorTick, d);
    }

    private void AnimateColor(CompositionColorBrush brush, Color target, TimeSpan duration)
    {
        var anim = _compositor.CreateColorKeyFrameAnimation();
        anim.InsertKeyFrame(1f, target);
        anim.Duration = duration;
        brush.StartAnimation(nameof(CompositionColorBrush.Color), anim);
    }

    public void SetScaleAndOpacity(float scale, float opacity)
    {
        _rootVisual.Opacity = Math.Clamp(opacity, 0.15f, 1.0f);
        float clampedScale = Math.Clamp(scale, 0.5f, 1.8f);
        if (MathF.Abs(_userScale - clampedScale) > 0.005f)
        {
            _userScale = clampedScale;
            _rootVisual.Scale = new Vector3(_userScale, _userScale, 1f);
            UpdateDpi(_baseDpiScale);
        }
    }

    public void UpdateDpi(float newBaseDpiScale)
    {
        _baseDpiScale = newBaseDpiScale;
        if (_textAtlas is null) return;
        _textAtlas.UpdateDpi(_baseDpiScale * _userScale);
        RebuildDigitMaskBrushes();
        if (_lastH1 >= 0)
        {
            _digitSprites[0].Brush = _hourMasks[_lastH1];
            _digitSprites[1].Brush = _hourMasks[_lastH2];
            _digitSprites[2].Brush = _minMasks[_lastM1];
            _digitSprites[3].Brush = _minMasks[_lastM2];
        }
    }

    public void Dispose()
    {
        _textAtlas?.Dispose();
    }
}

/// <summary>
/// 獨立、透明、無邊框之桌面時鐘小工具視窗外殼，負責處理 Win32 樣式、拖曳、縮放與右鍵選單。
/// </summary>
public sealed partial class ClockWidgetWindow : Window
{
    private const int CMD_TOGGLE_ZORDER = 3001;
    private const int CMD_TOGGLE_LOCK = 3002;
    private const int CMD_OPEN_SETTINGS = 3003;
    private const int CMD_CLOSE_WIDGET = 3099;

    private readonly nint _hwnd;
    private readonly ClockStyle _style;
    private readonly NativeMethods.SUBCLASSPROC _subclassDelegate;
    private readonly ClockWidgetView _view;
    private bool _isTopMost;

    public ClockWidgetWindow(ClockStyle style, int initialX, int initialY)
    {
        _style = style;
        Title = $"WinMaterialClock - {style}";
        SystemBackdrop = new TransparentBackdrop();

        _hwnd = WindowNative.GetWindowHandle(this);
        _subclassDelegate = WindowSubclassProc;

        ConfigureFramelessToolWindow(initialX, initialY);

        _view = new ClockWidgetView(this, style);
        Content = _view.Build();

        if (_style == ClockStyle.Pixel2x2)
        {
            NativeMethods.RegisterHotKey(
                _hwnd,
                NativeMethods.HOTKEY_ID_TOGGLE_LOCK,
                NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
                'L');
            NativeMethods.RegisterHotKey(
                _hwnd,
                NativeMethods.HOTKEY_ID_OPEN_SETTINGS,
                NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
                'S');
        }
    }

    /// <summary>配置無邊框、全透明、不佔工作列圖示 (WS_EX_TOOLWINDOW) 之底層視窗。</summary>
    private void ConfigureFramelessToolWindow(int x, int y)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        nint style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_STYLE);
        style &= ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME |
                   NativeMethods.WS_MINIMIZEBOX | NativeMethods.WS_MAXIMIZEBOX | NativeMethods.WS_SYSMENU);
        style |= NativeMethods.WS_POPUP;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_STYLE, style);

        nint exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle &= ~(NativeMethods.WS_EX_APPWINDOW | NativeMethods.WS_EX_TOPMOST);
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
        if (WidgetManager.IsClickThroughEnabled)
            exStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT;

        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

        nint shellWindow = NativeMethods.GetShellWindow();
        if (shellWindow != 0)
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWLP_HWNDPARENT, shellWindow);

        uint disablePolicy = NativeMethods.DWMNCRP_DISABLED;
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_NCRENDERING_POLICY, in disablePolicy, sizeof(uint));
        uint doNotRound = NativeMethods.DWMWCP_DONOTROUND;
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, in doNotRound, sizeof(uint));
        uint borderNone = NativeMethods.DWM_COLOR_NONE;
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_BORDER_COLOR, in borderNone, sizeof(uint));

        NativeMethods.MARGINS margins = new() { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        NativeMethods.DwmExtendFrameIntoClientArea(_hwnd, in margins);

        nint hRgn = NativeMethods.CreateRectRgn(-2, -2, -1, -1);
        NativeMethods.DWM_BLURBEHIND bb = new() { dwFlags = 0x3, fEnable = 1, hRgnBlur = hRgn, fTransitionOnMaximized = 0 };
        NativeMethods.DwmEnableBlurBehindWindow(_hwnd, in bb);
        NativeMethods.DeleteObject(hRgn);

        float dpi = GetDpiScale();
        float scale = WidgetManager.Settings.Scale;
        int w = (int)MathF.Ceiling(WidgetCompositionEngine.WidthDip * scale * dpi);
        int h = (int)MathF.Ceiling(WidgetCompositionEngine.HeightDip * scale * dpi);

        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_BOTTOM, x, y, w, h, NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
        NativeMethods.SetWindowSubclass(_hwnd, _subclassDelegate, 1, 0);
    }

    public float GetDpiScale()
    {
        uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
        return dpi > 0 ? dpi / 96f : 1.0f;
    }

    /// <summary>透過 P/Invoke SendMessage + WM_NCLBUTTONDOWN + HTCAPTION 啟動原生視窗拖曳。</summary>
    public void BeginNativeDrag()
    {
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(_hwnd, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
        if (_style == ClockStyle.Pixel2x2 && NativeMethods.GetWindowRect(_hwnd, out NativeMethods.RECT rc))
        {
            WidgetManager.Settings.WindowX = rc.Left;
            WidgetManager.Settings.WindowY = rc.Top;
            WidgetManager.Settings.Save();
        }
    }

    /// <summary>設定本視窗是否啟用穿透點擊 (WS_EX_TRANSPARENT)。</summary>
    public void SetClickThrough(bool enable)
    {
        nint exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE);
        if (enable)
            exStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT;
        else
            exStyle &= ~(NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT);

        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    /// <summary>同步調整視窗與 Composition 視覺之縮放倍率及不透明度。</summary>
    public void ApplyScaleAndOpacity(float scale, float opacity)
    {
        _view.UpdateScaleAndOpacity(scale, opacity);
        if (NativeMethods.GetWindowRect(_hwnd, out NativeMethods.RECT rc))
        {
            float dpi = GetDpiScale();
            int w = (int)MathF.Ceiling(WidgetCompositionEngine.WidthDip * scale * dpi);
            int h = (int)MathF.Ceiling(WidgetCompositionEngine.HeightDip * scale * dpi);
            NativeMethods.SetWindowPos(
                _hwnd,
                _isTopMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_BOTTOM,
                rc.Left, rc.Top, w, h,
                NativeMethods.SWP_NOACTIVATE);
        }
    }

    /// <summary>將本小工具視窗帶至前景。</summary>
    public void BringWidgetToFront()
    {
        NativeMethods.SetWindowPos(
            _hwnd,
            _isTopMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
        NativeMethods.SetForegroundWindow(_hwnd);
    }

    /// <summary>彈出右鍵選單：支援切換置頂/置底、鎖定位置（穿透點擊）、開啟設定中心、關閉本小工具。</summary>
    public void ShowWidgetContextMenu()
    {
        if (!NativeMethods.GetCursorPos(out NativeMethods.POINT pt))
            return;

        nint hMenu = NativeMethods.CreatePopupMenu();
        if (hMenu == 0) return;

        try
        {
            NativeMethods.AppendMenu(
                hMenu,
                NativeMethods.MF_STRING | (_isTopMost ? NativeMethods.MF_CHECKED : 0),
                CMD_TOGGLE_ZORDER,
                _isTopMost ? "層級：永遠置頂 (點擊切換為貼齊桌面底層)" : "層級：貼齊桌面底層 (點擊切換為永遠置頂)");

            NativeMethods.AppendMenu(
                hMenu,
                NativeMethods.MF_STRING | (WidgetManager.IsClickThroughEnabled ? NativeMethods.MF_CHECKED : 0),
                CMD_TOGGLE_LOCK,
                "鎖定位置 / 穿透點擊 (Alt+Shift+L)");

            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_SEPARATOR, 0, null);
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_OPEN_SETTINGS, "開啟設定中心... (Alt+Shift+S)");
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_SEPARATOR, 0, null);
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_CLOSE_WIDGET, "關閉本小工具");

            NativeMethods.SetForegroundWindow(_hwnd);
            int cmd = NativeMethods.TrackPopupMenuEx(
                hMenu,
                NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY,
                pt.X, pt.Y, _hwnd, 0);

            switch (cmd)
            {
                case CMD_TOGGLE_ZORDER:
                    _isTopMost = !_isTopMost;
                    NativeMethods.SetWindowPos(
                        _hwnd,
                        _isTopMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_BOTTOM,
                        0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                    break;
                case CMD_TOGGLE_LOCK:
                    WidgetManager.SetClickThroughAll(!WidgetManager.IsClickThroughEnabled);
                    break;
                case CMD_OPEN_SETTINGS:
                    SettingsCenterWindow.ShowInstance();
                    break;
                case CMD_CLOSE_WIDGET:
                    WidgetManager.RemoveWidget(_style);
                    break;
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(hMenu);
        }
    }

    private nint WindowSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        switch (uMsg)
        {
            case NativeMethods.WM_ERASEBKGND:
                NativeMethods.GetClientRect(hWnd, out NativeMethods.RECT rc);
                NativeMethods.FillRect(wParam, in rc, NativeMethods.GetStockObject(4));
                return 1;

            case NativeMethods.WM_WINDOWPOSCHANGING:
                if (!_isTopMost)
                {
                    unsafe
                    {
                        var wp = (NativeMethods.WINDOWPOS*)lParam;
                        wp->hwndInsertAfter = NativeMethods.HWND_BOTTOM;
                        wp->flags |= NativeMethods.SWP_NOACTIVATE;
                    }
                    return 0;
                }
                break;

            case NativeMethods.WM_NCHITTEST:
                return WidgetManager.IsClickThroughEnabled ? NativeMethods.HTTRANSPARENT : NativeMethods.HTCLIENT;

            case NativeMethods.WM_RBUTTONUP:
            case NativeMethods.WM_NCRBUTTONUP:
            case NativeMethods.WM_CONTEXTMENU:
                ShowWidgetContextMenu();
                return 0;

            case NativeMethods.WM_HOTKEY:
                int hotkeyId = (int)wParam;
                if (hotkeyId == NativeMethods.HOTKEY_ID_TOGGLE_LOCK)
                {
                    WidgetManager.SetClickThroughAll(!WidgetManager.IsClickThroughEnabled);
                    return 0;
                }
                if (hotkeyId == NativeMethods.HOTKEY_ID_OPEN_SETTINGS)
                {
                    SettingsCenterWindow.ShowInstance();
                    return 0;
                }
                break;

            case NativeMethods.WM_DWMCOLORIZATIONCOLORCHANGED:
            case NativeMethods.WM_SETTINGCHANGE:
                DispatcherQueue.TryEnqueue(_view.RefreshThemePalette);
                break;

            case NativeMethods.WM_POWERBROADCAST:
                DispatcherQueue.TryEnqueue(() => _view.SyncClockHandsOrDigits(animate: false));
                break;

            case NativeMethods.WM_DPICHANGED:
                float newScale = (wParam.ToInt32() & 0xFFFF) / 96f;
                _view.UpdateDpi(newScale);
                ApplyScaleAndOpacity(WidgetManager.Settings.Scale, WidgetManager.Settings.Opacity);
                return 0;

            case NativeMethods.WM_DESTROY:
                if (_style == ClockStyle.Pixel2x2)
                {
                    NativeMethods.UnregisterHotKey(hWnd, NativeMethods.HOTKEY_ID_TOGGLE_LOCK);
                    NativeMethods.UnregisterHotKey(hWnd, NativeMethods.HOTKEY_ID_OPEN_SETTINGS);
                }
                NativeMethods.RemoveWindowSubclass(hWnd, _subclassDelegate, 1);
                break;
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }
}
