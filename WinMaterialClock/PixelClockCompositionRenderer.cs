using System.Numerics;
using Microsoft.UI.Composition;
using Windows.UI;

namespace WinMaterialClock;

internal sealed partial class PixelClockCompositionRenderer : IDisposable
{
    public const int WidgetWidthDip = 260;
    public const int WidgetHeightDip = 300;
    private const int DigitWidthDip = 104;
    private const int DigitHeightDip = 128;

    private readonly Compositor _compositor;
    private readonly ContainerVisual _rootVisual;
    private readonly ContainerVisual _digitalContainer;
    private readonly ContainerVisual _analogContainer;

    private readonly ShapeVisual _cardBackgroundVisual;
    private readonly ShapeVisual _topPillVisual;
    private readonly ShapeVisual _bottomPillVisual;
    private readonly ShapeVisual _fluentBackgroundVisual;
    private readonly SpriteVisual[] _digitVisuals = new SpriteVisual[4];

    private readonly ShapeVisual _analogDialVisual;
    private readonly ShapeVisual _hourHandVisual;
    private readonly ShapeVisual _minuteHandVisual;
    private readonly ShapeVisual _secondHandVisual;

    private readonly CompositionColorBrush _hourColorBrush;
    private readonly CompositionColorBrush _minuteColorBrush;
    private readonly CompositionColorBrush _secondAccentBrush;
    private readonly CompositionColorBrush _cardColorBrush;
    private readonly CompositionColorBrush _pillColorBrush;
    private readonly CompositionColorBrush _fluentInnerBrush;
    private readonly CompositionColorBrush _minorTickBrush;
    private readonly CompositionLinearGradientBrush _fluentBorderBrush;
    private readonly CompositionColorGradientStop _fluentStopTop;
    private readonly CompositionColorGradientStop _fluentStopBottom;

    private readonly Direct2DTextAtlas _atlas;
    private readonly CompositionMaskBrush[] _hourDigitBrushes = new CompositionMaskBrush[10];
    private readonly CompositionMaskBrush[] _minuteDigitBrushes = new CompositionMaskBrush[10];

    private float _baseDpiScale;
    private float _userScale = 1.0f;
    private int _lastH1 = -1, _lastH2 = -1, _lastM1 = -1, _lastM2 = -1;
    private int _lastSecond = -1;
    private ClockVisualStyle _currentStyle = ClockVisualStyle.PixelStackedCard;

    public ContainerVisual RootVisual => _rootVisual;
    public ClockVisualStyle CurrentStyle => _currentStyle;

    public PixelClockCompositionRenderer(Compositor compositor, float initialDpiScale)
    {
        _compositor = compositor;
        _baseDpiScale = initialDpiScale;

        _rootVisual = _compositor.CreateContainerVisual();
        _rootVisual.Size = new Vector2(WidgetWidthDip, WidgetHeightDip);

        _hourColorBrush = _compositor.CreateColorBrush(Color.FromArgb(255, 208, 188, 255));
        _minuteColorBrush = _compositor.CreateColorBrush(Color.FromArgb(255, 168, 199, 250));
        _secondAccentBrush = _compositor.CreateColorBrush(Color.FromArgb(255, 255, 180, 168));
        _cardColorBrush = _compositor.CreateColorBrush(Color.FromArgb(225, 30, 26, 36));
        _pillColorBrush = _compositor.CreateColorBrush(Color.FromArgb(235, 48, 42, 60));
        _fluentInnerBrush = _compositor.CreateColorBrush(Color.FromArgb(210, 44, 40, 52));
        _minorTickBrush = _compositor.CreateColorBrush(Color.FromArgb(110, 168, 199, 250));

        _fluentBorderBrush = _compositor.CreateLinearGradientBrush();
        _fluentBorderBrush.StartPoint = Vector2.Zero;
        _fluentBorderBrush.EndPoint = Vector2.One;
        _fluentStopTop = _compositor.CreateColorGradientStop(0f, Color.FromArgb(150, 255, 255, 255));
        _fluentStopBottom = _compositor.CreateColorGradientStop(1f, Color.FromArgb(30, 255, 255, 255));
        _fluentBorderBrush.ColorStops.Add(_fluentStopTop);
        _fluentBorderBrush.ColorStops.Add(_fluentStopBottom);

        _cardBackgroundVisual = CreateRoundedRectShape(new Vector2(WidgetWidthDip, WidgetHeightDip), 44f, _cardColorBrush, null, 0f);
        _rootVisual.Children.InsertAtBottom(_cardBackgroundVisual);

        _topPillVisual = CreateRoundedRectShape(new Vector2(228, 128), 36f, _cardColorBrush, null, 0f);
        _topPillVisual.Offset = new Vector3(16, 16, 0);
        _topPillVisual.IsVisible = false;

        _bottomPillVisual = CreateRoundedRectShape(new Vector2(228, 128), 36f, _pillColorBrush, null, 0f);
        _bottomPillVisual.Offset = new Vector3(16, 156, 0);
        _bottomPillVisual.IsVisible = false;

        _rootVisual.Children.InsertAtTop(_topPillVisual);
        _rootVisual.Children.InsertAtTop(_bottomPillVisual);

        _fluentBackgroundVisual = CreateFluentCardVisual();
        _fluentBackgroundVisual.IsVisible = false;
        _rootVisual.Children.InsertAtTop(_fluentBackgroundVisual);

        _digitalContainer = _compositor.CreateContainerVisual();
        _digitalContainer.Size = new Vector2(WidgetWidthDip, WidgetHeightDip);
        _rootVisual.Children.InsertAtTop(_digitalContainer);

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
            sprite.Size = new Vector2(DigitWidthDip, DigitHeightDip);
            sprite.Offset = offsets[i];
            _digitVisuals[i] = sprite;
            _digitalContainer.Children.InsertAtTop(sprite);
        }

        _analogContainer = _compositor.CreateContainerVisual();
        _analogContainer.Size = new Vector2(WidgetWidthDip, WidgetHeightDip);
        _analogContainer.IsVisible = false;
        _rootVisual.Children.InsertAtTop(_analogContainer);

        _analogDialVisual = CreateAnalogDialVisual();
        _hourHandVisual = CreateHandVisual(width: 12f, length: 68f, tailOffset: 8f, _hourColorBrush);
        _minuteHandVisual = CreateHandVisual(width: 8f, length: 94f, tailOffset: 10f, _minuteColorBrush);
        _secondHandVisual = CreateSecondHandVisual();

        _analogContainer.Children.InsertAtTop(_analogDialVisual);
        _analogContainer.Children.InsertAtTop(_hourHandVisual);
        _analogContainer.Children.InsertAtTop(_minuteHandVisual);
        _analogContainer.Children.InsertAtTop(_secondHandVisual);

        _atlas = new Direct2DTextAtlas(compositor, DigitWidthDip, DigitHeightDip, initialDpiScale);
        BuildMaskBrushes();
    }

    private void BuildMaskBrushes()
    {
        for (int i = 0; i <= 9; i++)
        {
            var maskBrush = _atlas.GetDigitBrush(i);

            var hourBrush = _compositor.CreateMaskBrush();
            hourBrush.Mask = maskBrush;
            hourBrush.Source = _hourColorBrush;
            _hourDigitBrushes[i] = hourBrush;

            var minBrush = _compositor.CreateMaskBrush();
            minBrush.Mask = maskBrush;
            minBrush.Source = _minuteColorBrush;
            _minuteDigitBrushes[i] = minBrush;
        }

        if (_lastH1 >= 0)
        {
            _digitVisuals[0].Brush = _hourDigitBrushes[_lastH1];
            _digitVisuals[1].Brush = _hourDigitBrushes[_lastH2];
            _digitVisuals[2].Brush = _minuteDigitBrushes[_lastM1];
            _digitVisuals[3].Brush = _minuteDigitBrushes[_lastM2];
        }
    }

    private ShapeVisual CreateRoundedRectShape(
        Vector2 size,
        float cornerRadius,
        CompositionBrush fillBrush,
        CompositionBrush? strokeBrush,
        float strokeThickness)
    {
        var shapeVisual = _compositor.CreateShapeVisual();
        shapeVisual.Size = size;

        var geom = _compositor.CreateRoundedRectangleGeometry();
        geom.Size = new Vector2(size.X - strokeThickness, size.Y - strokeThickness);
        geom.Offset = new Vector2(strokeThickness * 0.5f, strokeThickness * 0.5f);
        geom.CornerRadius = new Vector2(cornerRadius, cornerRadius);

        var spriteShape = _compositor.CreateSpriteShape(geom);
        spriteShape.FillBrush = fillBrush;
        if (strokeBrush is not null && strokeThickness > 0f)
        {
            spriteShape.StrokeBrush = strokeBrush;
            spriteShape.StrokeThickness = strokeThickness;
        }

        shapeVisual.Shapes.Add(spriteShape);
        return shapeVisual;
    }

    private ShapeVisual CreateFluentCardVisual()
    {
        var size = new Vector2(WidgetWidthDip, WidgetHeightDip);
        var shapeVisual = _compositor.CreateShapeVisual();
        shapeVisual.Size = size;

        // 1. 外層毛玻璃感圓角容器 + 1.8px 頂部與左側斜向高光邊框 (Fluent Specular Border)
        var outerGeom = _compositor.CreateRoundedRectangleGeometry();
        outerGeom.Size = new Vector2(size.X - 2f, size.Y - 2f);
        outerGeom.Offset = new Vector2(1f, 1f);
        outerGeom.CornerRadius = new Vector2(28f, 28f);

        var outerShape = _compositor.CreateSpriteShape(outerGeom);
        outerShape.FillBrush = _cardColorBrush;
        outerShape.StrokeBrush = _fluentBorderBrush;
        outerShape.StrokeThickness = 1.8f;
        shapeVisual.Shapes.Add(outerShape);

        // 2. 上層內嵌方塊 (小時區)
        var topGeom = _compositor.CreateRoundedRectangleGeometry();
        topGeom.Size = new Vector2(224f, 120f);
        topGeom.Offset = new Vector2(18f, 18f);
        topGeom.CornerRadius = new Vector2(18f, 18f);
        var topShape = _compositor.CreateSpriteShape(topGeom);
        topShape.FillBrush = _fluentInnerBrush;
        topShape.StrokeBrush = _fluentBorderBrush;
        topShape.StrokeThickness = 1.0f;
        shapeVisual.Shapes.Add(topShape);

        // 3. 下層內嵌方塊 (分鐘區)
        var bottomGeom = _compositor.CreateRoundedRectangleGeometry();
        bottomGeom.Size = new Vector2(224f, 120f);
        bottomGeom.Offset = new Vector2(18f, 162f);
        bottomGeom.CornerRadius = new Vector2(18f, 18f);
        var bottomShape = _compositor.CreateSpriteShape(bottomGeom);
        bottomShape.FillBrush = _fluentInnerBrush;
        bottomShape.StrokeBrush = _fluentBorderBrush;
        bottomShape.StrokeThickness = 1.0f;
        shapeVisual.Shapes.Add(bottomShape);

        // 4. 中央 Fluent 強調色膠囊飾條
        var pillGeom = _compositor.CreateRoundedRectangleGeometry();
        pillGeom.Size = new Vector2(52f, 4f);
        pillGeom.Offset = new Vector2(104f, 148f);
        pillGeom.CornerRadius = new Vector2(2f, 2f);
        var pillShape = _compositor.CreateSpriteShape(pillGeom);
        pillShape.FillBrush = _secondAccentBrush;
        shapeVisual.Shapes.Add(pillShape);

        return shapeVisual;
    }

    private ShapeVisual CreateAnalogDialVisual()
    {
        var size = new Vector2(WidgetWidthDip, WidgetHeightDip);
        var shapeVisual = _compositor.CreateShapeVisual();
        shapeVisual.Size = size;

        var center = new Vector2(130f, 150f);
        const float outerRadius = 122f;

        // 1. 錶盤主圓形底座 + 外環高光邊框
        var dialGeom = _compositor.CreateEllipseGeometry();
        dialGeom.Center = center;
        dialGeom.Radius = new Vector2(outerRadius, outerRadius);
        var dialShape = _compositor.CreateSpriteShape(dialGeom);
        dialShape.FillBrush = _cardColorBrush;
        dialShape.StrokeBrush = _fluentBorderBrush;
        dialShape.StrokeThickness = 2.0f;
        shapeVisual.Shapes.Add(dialShape);

        // 2. 內圈裝飾環
        var innerRingGeom = _compositor.CreateEllipseGeometry();
        innerRingGeom.Center = center;
        innerRingGeom.Radius = new Vector2(96f, 96f);
        var innerRingShape = _compositor.CreateSpriteShape(innerRingGeom);
        innerRingShape.FillBrush = _fluentInnerBrush;
        shapeVisual.Shapes.Add(innerRingShape);

        // 3. 繪製 60 格刻度 (12/3/6/9 為主刻度，其餘小時為中刻度，分鐘為細刻度)
        for (int i = 0; i < 60; i++)
        {
            float rad = (i * 6f - 90f) * (MathF.PI / 180f);
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);

            bool isCardinal = (i % 15) == 0;
            bool isHour = (i % 5) == 0;

            float rOuter = 113f;
            float rInner = isCardinal ? 95f : (isHour ? 100f : 106f);
            float thickness = isCardinal ? 4.2f : (isHour ? 2.6f : 1.3f);

            var lineGeom = _compositor.CreateLineGeometry();
            lineGeom.Start = new Vector2(center.X + cos * rInner, center.Y + sin * rInner);
            lineGeom.End = new Vector2(center.X + cos * rOuter, center.Y + sin * rOuter);

            var tickShape = _compositor.CreateSpriteShape(lineGeom);
            tickShape.StrokeThickness = thickness;
            tickShape.StrokeStartCap = CompositionStrokeCap.Round;
            tickShape.StrokeEndCap = CompositionStrokeCap.Round;
            tickShape.StrokeBrush = isCardinal ? _hourColorBrush : (isHour ? _minuteColorBrush : _minorTickBrush);
            shapeVisual.Shapes.Add(tickShape);
        }

        return shapeVisual;
    }

    private ShapeVisual CreateHandVisual(float width, float length, float tailOffset, CompositionBrush brush)
    {
        var shapeVisual = _compositor.CreateShapeVisual();
        shapeVisual.Size = new Vector2(WidgetWidthDip, WidgetHeightDip);
        shapeVisual.CenterPoint = new Vector3(130f, 150f, 0f);

        var geom = _compositor.CreateRoundedRectangleGeometry();
        geom.Size = new Vector2(width, length);
        geom.Offset = new Vector2(130f - width * 0.5f, 150f - (length - tailOffset));
        geom.CornerRadius = new Vector2(width * 0.5f, width * 0.5f);

        var shape = _compositor.CreateSpriteShape(geom);
        shape.FillBrush = brush;
        shapeVisual.Shapes.Add(shape);
        return shapeVisual;
    }

    private ShapeVisual CreateSecondHandVisual()
    {
        var shapeVisual = _compositor.CreateShapeVisual();
        shapeVisual.Size = new Vector2(WidgetWidthDip, WidgetHeightDip);
        shapeVisual.CenterPoint = new Vector3(130f, 150f, 0f);

        var needleGeom = _compositor.CreateRoundedRectangleGeometry();
        needleGeom.Size = new Vector2(2.8f, 118f);
        needleGeom.Offset = new Vector2(130f - 1.4f, 150f - 100f);
        needleGeom.CornerRadius = new Vector2(1.4f, 1.4f);
        var needleShape = _compositor.CreateSpriteShape(needleGeom);
        needleShape.FillBrush = _secondAccentBrush;
        shapeVisual.Shapes.Add(needleShape);

        var tailCircle = _compositor.CreateEllipseGeometry();
        tailCircle.Center = new Vector2(130f, 168f);
        tailCircle.Radius = new Vector2(4.2f, 4.2f);
        var tailShape = _compositor.CreateSpriteShape(tailCircle);
        tailShape.FillBrush = _secondAccentBrush;
        shapeVisual.Shapes.Add(tailShape);

        var capOuter = _compositor.CreateEllipseGeometry();
        capOuter.Center = new Vector2(130f, 150f);
        capOuter.Radius = new Vector2(7.5f, 7.5f);
        var capOuterShape = _compositor.CreateSpriteShape(capOuter);
        capOuterShape.FillBrush = _secondAccentBrush;
        shapeVisual.Shapes.Add(capOuterShape);

        var capInner = _compositor.CreateEllipseGeometry();
        capInner.Center = new Vector2(130f, 150f);
        capInner.Radius = new Vector2(3.2f, 3.2f);
        var capInnerShape = _compositor.CreateSpriteShape(capInner);
        capInnerShape.FillBrush = _cardColorBrush;
        shapeVisual.Shapes.Add(capInnerShape);

        return shapeVisual;
    }

    public void UpdateDpi(float newBaseDpiScale)
    {
        _baseDpiScale = newBaseDpiScale;
        _atlas.UpdateDpi(_baseDpiScale * _userScale);
        BuildMaskBrushes();
    }

    public void SetScale(float scale)
    {
        float clamped = Math.Clamp(scale, 0.5f, 1.8f);
        if (MathF.Abs(_userScale - clamped) < 0.005f)
            return;

        _userScale = clamped;
        _rootVisual.Scale = new Vector3(_userScale, _userScale, 1f);
        _atlas.UpdateDpi(_baseDpiScale * _userScale);
        BuildMaskBrushes();
    }

    public void SetOpacity(float opacity)
    {
        _rootVisual.Opacity = Math.Clamp(opacity, 0.15f, 1.0f);
    }

    public void UpdateTime(int hour, int minute, int second, bool animate)
    {
        int h1 = hour / 10;
        int h2 = hour % 10;
        int m1 = minute / 10;
        int m2 = minute % 10;

        bool digitalAnimate = animate && _currentStyle != ClockVisualStyle.CircularAnalog;
        UpdateSingleDigit(0, h1, ref _lastH1, _hourDigitBrushes[h1], 16f, digitalAnimate);
        UpdateSingleDigit(1, h2, ref _lastH2, _hourDigitBrushes[h2], 16f, digitalAnimate);
        UpdateSingleDigit(2, m1, ref _lastM1, _minuteDigitBrushes[m1], 152f, digitalAnimate);
        UpdateSingleDigit(3, m2, ref _lastM2, _minuteDigitBrushes[m2], 152f, digitalAnimate);

        float hourAngle = (hour % 12) * 30f + minute * 0.5f + second * (0.5f / 60f);
        float minuteAngle = minute * 6f + second * 0.1f;
        float secondAngle = second * 6f;

        _hourHandVisual.RotationAngleInDegrees = hourAngle;
        _minuteHandVisual.RotationAngleInDegrees = minuteAngle;

        if (_currentStyle == ClockVisualStyle.CircularAnalog && animate && _lastSecond >= 0 && second != _lastSecond)
        {
            float fromAngle = second == 0 ? 354f : (second - 1) * 6f;
            float toAngle = second == 0 ? 360f : secondAngle;

            var easing = _compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.9f), new Vector2(0.3f, 1.0f));
            var secAnim = _compositor.CreateScalarKeyFrameAnimation();
            secAnim.InsertKeyFrame(0f, fromAngle);
            secAnim.InsertKeyFrame(1f, toAngle, easing);
            secAnim.Duration = TimeSpan.FromMilliseconds(150);
            _secondHandVisual.StartAnimation(nameof(Visual.RotationAngleInDegrees), secAnim);
        }
        else
        {
            _secondHandVisual.RotationAngleInDegrees = secondAngle;
        }

        _lastSecond = second;
    }

    private void UpdateSingleDigit(
        int index,
        int newDigit,
        ref int lastDigit,
        CompositionMaskBrush targetBrush,
        float baseY,
        bool animate)
    {
        if (lastDigit == newDigit)
            return;

        lastDigit = newDigit;
        SpriteVisual visual = _digitVisuals[index];
        visual.Brush = targetBrush;

        if (!animate)
            return;

        var easing = _compositor.CreateCubicBezierEasingFunction(new Vector2(0.05f, 0.7f), new Vector2(0.1f, 1.0f));
        var duration = TimeSpan.FromMilliseconds(300);

        var opacityAnim = _compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0f, 0.15f);
        opacityAnim.InsertKeyFrame(1f, 1.0f, easing);
        opacityAnim.Duration = duration;

        float x = visual.Offset.X;
        var offsetAnim = _compositor.CreateVector3KeyFrameAnimation();
        offsetAnim.InsertKeyFrame(0f, new Vector3(x, baseY + 12f, 0f));
        offsetAnim.InsertKeyFrame(1f, new Vector3(x, baseY, 0f), easing);
        offsetAnim.Duration = duration;

        visual.StartAnimation(nameof(Visual.Opacity), opacityAnim);
        visual.StartAnimation(nameof(Visual.Offset), offsetAnim);
    }

    public void ApplyPalette(MaterialPalette palette, bool animateTransition)
    {
        Color minorTick = Color.FromArgb(105, palette.MinuteDigits.R, palette.MinuteDigits.G, palette.MinuteDigits.B);
        _fluentStopTop.Color = palette.FluentBorderTop;
        _fluentStopBottom.Color = palette.FluentBorderBottom;

        if (!animateTransition)
        {
            _hourColorBrush.Color = palette.HourDigits;
            _minuteColorBrush.Color = palette.MinuteDigits;
            _secondAccentBrush.Color = palette.SecondHandAccent;
            _cardColorBrush.Color = palette.CardSurface;
            _pillColorBrush.Color = palette.PillAccent;
            _fluentInnerBrush.Color = palette.FluentInnerSurface;
            _minorTickBrush.Color = minorTick;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(420);
        AnimateColorBrush(_hourColorBrush, palette.HourDigits, duration);
        AnimateColorBrush(_minuteColorBrush, palette.MinuteDigits, duration);
        AnimateColorBrush(_secondAccentBrush, palette.SecondHandAccent, duration);
        AnimateColorBrush(_cardColorBrush, palette.CardSurface, duration);
        AnimateColorBrush(_pillColorBrush, palette.PillAccent, duration);
        AnimateColorBrush(_fluentInnerBrush, palette.FluentInnerSurface, duration);
        AnimateColorBrush(_minorTickBrush, minorTick, duration);
    }

    private void AnimateColorBrush(CompositionColorBrush brush, Color targetColor, TimeSpan duration)
    {
        var anim = _compositor.CreateColorKeyFrameAnimation();
        anim.InsertKeyFrame(1f, targetColor);
        anim.Duration = duration;
        brush.StartAnimation(nameof(CompositionColorBrush.Color), anim);
    }

    public void SetVisualStyle(ClockVisualStyle style)
    {
        _currentStyle = style;
        bool isAnalog = style == ClockVisualStyle.CircularAnalog;

        _digitalContainer.IsVisible = !isAnalog;
        _analogContainer.IsVisible = isAnalog;

        _cardBackgroundVisual.IsVisible = style == ClockVisualStyle.PixelStackedCard;
        _topPillVisual.IsVisible = style == ClockVisualStyle.TonalPills;
        _bottomPillVisual.IsVisible = style == ClockVisualStyle.TonalPills;
        _fluentBackgroundVisual.IsVisible = style == ClockVisualStyle.FluentDesign;
    }

    public void Dispose()
    {
        _atlas.Dispose();
    }
}
