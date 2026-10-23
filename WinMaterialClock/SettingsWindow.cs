using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.Text;

namespace WinMaterialClock;

internal sealed partial class SettingsWindow : Window
{
    private readonly MainWindow _owner;
    private readonly AppSettings _settings;
    private readonly Border[] _styleCards = new Border[5];
    private TextBlock? _scaleValueText;
    private TextBlock? _opacityValueText;
    private Border? _lockToggleBadge;
    private TextBlock? _lockToggleText;
    private Border? _startupToggleBadge;
    private TextBlock? _startupToggleText;

    private readonly SolidColorBrush _accentBrush;
    private readonly SolidColorBrush _accentSubtleBrush;
    private readonly SolidColorBrush _cardBgBrush;
    private readonly SolidColorBrush _cardBorderBrush;
    private readonly SolidColorBrush _textPrimaryBrush;
    private readonly SolidColorBrush _textSecondaryBrush;

    public SettingsWindow(MainWindow owner, AppSettings settings, MaterialPalette palette)
    {
        _owner = owner;
        _settings = settings;

        Title = "WinMaterialClock 設定";
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;

        bool isDark = palette.IsDarkTheme;
        _accentBrush = new SolidColorBrush(palette.HourDigits);
        _accentSubtleBrush = new SolidColorBrush(Color.FromArgb(55, palette.HourDigits.R, palette.HourDigits.G, palette.HourDigits.B));
        _cardBgBrush = new SolidColorBrush(isDark ? Color.FromArgb(140, 38, 38, 44) : Color.FromArgb(180, 255, 255, 255));
        _cardBorderBrush = new SolidColorBrush(isDark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(30, 0, 0, 0));
        _textPrimaryBrush = new SolidColorBrush(isDark ? Colors.White : Color.FromArgb(255, 24, 24, 28));
        _textSecondaryBrush = new SolidColorBrush(isDark ? Color.FromArgb(185, 210, 210, 220) : Color.FromArgb(185, 80, 80, 95));

        AppWindow.Resize(new SizeInt32(520, 680));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }

        var rootGrid = new Grid
        {
            Padding = new Thickness(24, 36, 24, 24),
            RowSpacing = 14
        };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headerStack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 4) };
        headerStack.Children.Add(new TextBlock
        {
            Text = "WinMaterialClock 桌面時鐘設定",
            FontSize = 22,
            FontWeight = new FontWeight(700),
            Foreground = _textPrimaryBrush
        });
        headerStack.Children.Add(new TextBlock
        {
            Text = "固定於桌面底層，不遮擋日常工作視窗 • 即時套用設定",
            FontSize = 12.5,
            Foreground = _textSecondaryBrush
        });
        Grid.SetRow(headerStack, 0);
        rootGrid.Children.Add(headerStack);

        var styleCardContainer = CreateSectionCard("時鐘視覺樣式");
        var styleStack = (StackPanel)styleCardContainer.Child;

        var styleGrid = new Grid { ColumnSpacing = 8, RowSpacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        styleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        styleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        styleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        styleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        styleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddStyleOption(styleGrid, 0, 0, 1, ClockVisualStyle.PixelStackedCard, "Pixel 圓角卡片", "經典 Material You 2x2 圓角底板");
        AddStyleOption(styleGrid, 0, 1, 1, ClockVisualStyle.TonalPills, "Material 雙色膠囊", "上下獨立雙色調膠囊容器");
        AddStyleOption(styleGrid, 1, 0, 1, ClockVisualStyle.CircularAnalog, "圓形指針錶盤", "60 格精細刻度 + 時、分、秒三針");
        AddStyleOption(styleGrid, 1, 1, 1, ClockVisualStyle.FluentDesign, "Fluent Design", "Win11 分層毛玻璃與高光斜角邊框");
        AddStyleOption(styleGrid, 2, 0, 2, ClockVisualStyle.PureFloatingText, "極簡無底板懸浮", "移除所有背景，僅保留純粹粗體數字");

        styleStack.Children.Add(styleGrid);
        Grid.SetRow(styleCardContainer, 1);
        rootGrid.Children.Add(styleCardContainer);

        BuildRemainingSections(rootGrid);
        Content = rootGrid;
        UpdateStyleSelectionVisuals();
    }

    private void BuildRemainingSections(Grid rootGrid)
    {
        var slidersCard = CreateSectionCard("尺寸與透明度");
        var slidersStack = (StackPanel)slidersCard.Child;

        slidersStack.Children.Add(CreateCustomSliderRow(
            "時鐘縮放大小",
            min: 0.50f, max: 1.80f, initial: _settings.Scale,
            out _scaleValueText,
            val =>
            {
                _scaleValueText!.Text = string.Create(CultureInfo.InvariantCulture, $"{(int)MathF.Round(val * 100f)}%");
                _owner.UpdateScaleFromSettings(val);
            }));

        slidersStack.Children.Add(CreateCustomSliderRow(
            "整體不透明度",
            min: 0.15f, max: 1.00f, initial: _settings.Opacity,
            out _opacityValueText,
            val =>
            {
                _opacityValueText!.Text = string.Create(CultureInfo.InvariantCulture, $"{(int)MathF.Round(val * 100f)}%");
                _owner.UpdateOpacityFromSettings(val);
            }));

        Grid.SetRow(slidersCard, 2);
        rootGrid.Children.Add(slidersCard);

        var togglesCard = CreateSectionCard("桌面互動與系統");
        var togglesStack = (StackPanel)togglesCard.Child;

        togglesStack.Children.Add(CreateToggleRow(
            "鎖定位置並穿透點擊",
            "開啟後滑鼠點擊直接穿透至桌面，按 Alt + Shift + L 可隨時解鎖",
            _settings.IsLockedClickThrough,
            out _lockToggleBadge,
            out _lockToggleText,
            () =>
            {
                _owner.ToggleClickThroughLock();
                UpdateToggleVisual(_lockToggleBadge!, _lockToggleText!, _settings.IsLockedClickThrough);
            }));

        bool isStartup = StartupManager.IsStartupEnabled();
        togglesStack.Children.Add(CreateToggleRow(
            "開機自動啟動",
            "登入 Windows 時自動於桌面底層啟動時鐘",
            isStartup,
            out _startupToggleBadge,
            out _startupToggleText,
            () =>
            {
                bool next = !StartupManager.IsStartupEnabled();
                StartupManager.SetStartup(next);
                UpdateToggleVisual(_startupToggleBadge!, _startupToggleText!, next);
            }));

        Grid.SetRow(togglesCard, 3);
        rootGrid.Children.Add(togglesCard);

        var footerGrid = BuildFooterButtons();
        Grid.SetRow(footerGrid, 4);
        rootGrid.Children.Add(footerGrid);
    }

    private Border CreateSectionCard(string sectionTitle)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = sectionTitle,
            FontSize = 13.5,
            FontWeight = new FontWeight(600),
            Foreground = _accentBrush
        });

        return new Border
        {
            Background = _cardBgBrush,
            BorderBrush = _cardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 16, 14),
            Child = stack
        };
    }

    private void AddStyleOption(
        Grid parentGrid,
        int row,
        int col,
        int colSpan,
        ClockVisualStyle style,
        string title,
        string subtitle)
    {
        var textStack = new StackPanel { Spacing = 2 };
        textStack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13.5,
            FontWeight = new FontWeight(600),
            Foreground = _textPrimaryBrush
        });
        textStack.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 11.5,
            Foreground = _textSecondaryBrush,
            TextWrapping = TextWrapping.Wrap
        });

        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(12, 8, 12, 8),
            Child = textStack
        };

        card.PointerPressed += (_, _) =>
        {
            _owner.UpdateStyleFromSettings(style);
            UpdateStyleSelectionVisuals();
        };

        _styleCards[(int)style] = card;
        Grid.SetRow(card, row);
        Grid.SetColumn(card, col);
        if (colSpan > 1)
            Grid.SetColumnSpan(card, colSpan);

        parentGrid.Children.Add(card);
    }

    public void UpdateStyleSelectionVisuals()
    {
        int activeIdx = (int)_settings.Style;
        for (int i = 0; i < _styleCards.Length; i++)
        {
            Border? card = _styleCards[i];
            if (card is null) continue;

            bool isSelected = i == activeIdx;
            card.Background = isSelected ? _accentSubtleBrush : new SolidColorBrush(Colors.Transparent);
            card.BorderBrush = isSelected ? _accentBrush : _cardBorderBrush;
        }
    }

    public void RefreshLockState()
    {
        if (_lockToggleBadge is not null && _lockToggleText is not null)
            UpdateToggleVisual(_lockToggleBadge, _lockToggleText, _settings.IsLockedClickThrough);
    }

    private StackPanel CreateCustomSliderRow(
        string label,
        float min,
        float max,
        float initial,
        out TextBlock valueLabel,
        Action<float> onValueChanged)
    {
        var rowStack = new StackPanel { Spacing = 6, Margin = new Thickness(0, 2, 0, 2) };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleText = new TextBlock
        {
            Text = label,
            FontSize = 13.5,
            FontWeight = new FontWeight(500),
            Foreground = _textPrimaryBrush
        };
        valueLabel = new TextBlock
        {
            Text = string.Create(CultureInfo.InvariantCulture, $"{(int)MathF.Round(initial * 100f)}%"),
            FontSize = 13,
            FontWeight = new FontWeight(600),
            Foreground = _accentBrush
        };
        Grid.SetColumn(titleText, 0);
        Grid.SetColumn(valueLabel, 1);
        headerGrid.Children.Add(titleText);
        headerGrid.Children.Add(valueLabel);
        rowStack.Children.Add(headerGrid);

        const double trackWidth = 432.0;
        const double thumbSize = 18.0;

        var trackHost = new Canvas
        {
            Width = trackWidth,
            Height = 24,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Colors.Transparent)
        };

        var bgTrack = new Rectangle
        {
            Width = trackWidth,
            Height = 6,
            RadiusX = 3,
            RadiusY = 3,
            Fill = _cardBorderBrush
        };
        Canvas.SetTop(bgTrack, 9);
        trackHost.Children.Add(bgTrack);

        double initialNorm = Math.Clamp((initial - min) / (max - min), 0.0, 1.0);
        var activeTrack = new Rectangle
        {
            Width = Math.Max(6.0, initialNorm * trackWidth),
            Height = 6,
            RadiusX = 3,
            RadiusY = 3,
            Fill = _accentBrush
        };
        Canvas.SetTop(activeTrack, 9);
        trackHost.Children.Add(activeTrack);

        var thumb = new Ellipse
        {
            Width = thumbSize,
            Height = thumbSize,
            Fill = _accentBrush,
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 2
        };
        Canvas.SetTop(thumb, 3);
        Canvas.SetLeft(thumb, initialNorm * (trackWidth - thumbSize));
        trackHost.Children.Add(thumb);

        bool isDragging = false;
        void UpdateFromX(double x)
        {
            double norm = Math.Clamp(x / trackWidth, 0.0, 1.0);
            activeTrack.Width = Math.Max(6.0, norm * trackWidth);
            Canvas.SetLeft(thumb, norm * (trackWidth - thumbSize));
            float val = min + (float)norm * (max - min);
            onValueChanged(val);
        }

        trackHost.PointerPressed += (s, e) =>
        {
            isDragging = true;
            trackHost.CapturePointer(e.Pointer);
            UpdateFromX(e.GetCurrentPoint(trackHost).Position.X);
        };
        trackHost.PointerMoved += (s, e) =>
        {
            if (isDragging)
                UpdateFromX(e.GetCurrentPoint(trackHost).Position.X);
        };
        trackHost.PointerReleased += (s, e) =>
        {
            if (isDragging)
            {
                isDragging = false;
                trackHost.ReleasePointerCapture(e.Pointer);
                _settings.Save();
            }
        };

        rowStack.Children.Add(trackHost);
        return rowStack;
    }

    private Border CreateToggleRow(
        string title,
        string subtitle,
        bool initialState,
        out Border badgeBorder,
        out TextBlock badgeText,
        Action onToggle)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13.5,
            FontWeight = new FontWeight(600),
            Foreground = _textPrimaryBrush
        });
        textStack.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 11.5,
            Foreground = _textSecondaryBrush,
            TextWrapping = TextWrapping.Wrap
        });

        badgeText = new TextBlock
        {
            FontSize = 12,
            FontWeight = new FontWeight(600),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        badgeBorder = new Border
        {
            MinWidth = 64,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Padding = new Thickness(12, 0, 12, 0),
            BorderThickness = new Thickness(1.5),
            VerticalAlignment = VerticalAlignment.Center,
            Child = badgeText
        };

        UpdateToggleVisual(badgeBorder, badgeText, initialState);

        Grid.SetColumn(textStack, 0);
        Grid.SetColumn(badgeBorder, 1);
        grid.Children.Add(textStack);
        grid.Children.Add(badgeBorder);

        var rowContainer = new Border
        {
            Background = new SolidColorBrush(Colors.Transparent),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4, 4, 4, 4),
            Child = grid
        };
        rowContainer.PointerPressed += (_, _) => onToggle();
        return rowContainer;
    }

    private void UpdateToggleVisual(Border badge, TextBlock text, bool isOn)
    {
        badge.Background = isOn ? _accentSubtleBrush : new SolidColorBrush(Colors.Transparent);
        badge.BorderBrush = isOn ? _accentBrush : _cardBorderBrush;
        text.Text = isOn ? "已開啟" : "已關閉";
        text.Foreground = isOn ? _accentBrush : _textSecondaryBrush;
    }

    private Grid BuildFooterButtons()
    {
        var grid = new Grid { ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Bottom };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var resetBtn = new Border
        {
            Background = _cardBgBrush,
            BorderBrush = _cardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Child = new TextBlock
            {
                Text = "重置時鐘位置 (左上角)",
                FontSize = 13,
                FontWeight = new FontWeight(600),
                Foreground = _textPrimaryBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        resetBtn.PointerPressed += (_, _) => _owner.ResetClockPosition();

        var exitBtn = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(45, 235, 87, 87)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(140, 235, 87, 87)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Child = new TextBlock
            {
                Text = "離開 WinMaterialClock",
                FontSize = 13,
                FontWeight = new FontWeight(600),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 110, 110)),
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        exitBtn.PointerPressed += (_, _) => _owner.ExitApplication();

        Grid.SetColumn(resetBtn, 0);
        Grid.SetColumn(exitBtn, 1);
        grid.Children.Add(resetBtn);
        grid.Children.Add(exitBtn);
        return grid;
    }
}
