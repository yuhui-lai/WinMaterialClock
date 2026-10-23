using System.Runtime.InteropServices;
using Microsoft.Graphics.DirectX;
using Microsoft.UI.Composition;
using Windows.Foundation;
using WinRT;

namespace WinMaterialClock;

internal sealed unsafe partial class Direct2DTextAtlas : IDisposable
{
    private readonly Compositor _compositor;
    private readonly int _digitWidthDip;
    private readonly int _digitHeightDip;
    private float _dpiScale;

    private CompositionGraphicsDevice? _graphicsDevice;
    private nint _d2dFactory;
    private nint _dwriteFactory;
    private nint _d2dDevice;

    private readonly CompositionSurfaceBrush[] _digitMaskBrushes = new CompositionSurfaceBrush[10];

    [StructLayout(LayoutKind.Sequential)]
    private struct D2D1_COLOR_F
    {
        public float r, g, b, a;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D2D1_RECT_F
    {
        public float left, top, right, bottom;
    }

    public Direct2DTextAtlas(Compositor compositor, int digitWidthDip, int digitHeightDip, float dpiScale)
    {
        _compositor = compositor;
        _digitWidthDip = digitWidthDip;
        _digitHeightDip = digitHeightDip;
        _dpiScale = dpiScale;

        InitializeDirect2D();
        RebuildSurfaces();
    }

    public CompositionSurfaceBrush GetDigitBrush(int digit) => _digitMaskBrushes[digit];

    public void UpdateDpi(float newDpiScale)
    {
        if (MathF.Abs(_dpiScale - newDpiScale) < 0.01f)
            return;

        _dpiScale = newDpiScale;
        RebuildSurfaces();
    }
    private void InitializeDirect2D()
    {
        Guid iidD2DFactory = new("bb12d362-daee-4b9a-aa1d-14ba401cfa1f"); // ID2D1Factory1
        NativeMethods.D2D1CreateFactory(0, in iidD2DFactory, 0, out _d2dFactory);

        Guid iidDWriteFactory = new("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
        NativeMethods.DWriteCreateFactory(0, in iidDWriteFactory, out _dwriteFactory);

        const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
        const int D3D_DRIVER_TYPE_HARDWARE = 1;
        NativeMethods.D3D11CreateDevice(
            0, D3D_DRIVER_TYPE_HARDWARE, 0, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            0, 0, 7, out nint d3dDevice, out _, out nint d3dContext);

        try
        {
            Guid iidDxgiDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
            Marshal.QueryInterface(d3dDevice, in iidDxgiDevice, out nint dxgiDevice);
            try
            {
                var createDevice = (delegate* unmanaged[Stdcall]<nint, nint, out nint, int>)(*(*(void***)_d2dFactory + 17));
                createDevice(_d2dFactory, dxgiDevice, out _d2dDevice);

                Guid iidCompositorInterop = new("FAB19398-6D19-4D8A-B752-8F096C396069");
                nint pCompositor = ((IWinRTObject)_compositor).NativeObject.ThisPtr;
                int qiHr = Marshal.QueryInterface(pCompositor, in iidCompositorInterop, out nint compositorInterop);
                if (qiHr >= 0 && compositorInterop != 0)
                {
                    try
                    {
                        var createGraphicsDevice = (delegate* unmanaged[Stdcall]<nint, nint, out nint, int>)(*(*(void***)compositorInterop + 3));
                        int cgHr = createGraphicsDevice(compositorInterop, _d2dDevice, out nint nativeGraphicsDevice);
                        if (cgHr >= 0 && nativeGraphicsDevice != 0)
                        {
                            try
                            {
                                _graphicsDevice = MarshalInterface<CompositionGraphicsDevice>.FromAbi(nativeGraphicsDevice);
                            }
                            finally
                            {
                                Marshal.Release(nativeGraphicsDevice);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.Release(compositorInterop);
                    }
                }
            }
            finally
            {
                Marshal.Release(dxgiDevice);
            }
        }
        finally
        {
            if (d3dContext != 0) Marshal.Release(d3dContext);
            if (d3dDevice != 0) Marshal.Release(d3dDevice);
        }
    }

    private void RebuildSurfaces()
    {
        if (_graphicsDevice is null)
            return;

        int pixelW = (int)MathF.Ceiling(_digitWidthDip * _dpiScale);
        int pixelH = (int)MathF.Ceiling(_digitHeightDip * _dpiScale);

        nint textFormat = CreateTextFormat(108f * _dpiScale);
        try
        {
            for (int digit = 0; digit <= 9; digit++)
            {
                var surface = _graphicsDevice.CreateDrawingSurface(
                    new Size(pixelW, pixelH),
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    DirectXAlphaMode.Premultiplied);

                DrawDigitMaskToSurface(surface, digit, pixelW, pixelH, textFormat);

                var maskSurfaceBrush = _compositor.CreateSurfaceBrush(surface);
                maskSurfaceBrush.Stretch = CompositionStretch.Fill;
                _digitMaskBrushes[digit] = maskSurfaceBrush;
            }
        }
        finally
        {
            if (textFormat != 0) Marshal.Release(textFormat);
        }
    }

    private nint CreateTextFormat(float fontSizePx)
    {
        var createTextFormat = (delegate* unmanaged[Stdcall]<
            nint, char*, nint, int, int, int, float, char*, out nint, int>)(*(*(void***)_dwriteFactory + 15));

        fixed (char* fontFamily = "Segoe UI Variable Display")
        fixed (char* locale = "en-us")
        {
            const int DWRITE_FONT_WEIGHT_EXTRA_BOLD = 800;
            const int DWRITE_FONT_STYLE_NORMAL = 0;
            const int DWRITE_FONT_STRETCH_NORMAL = 5;

            createTextFormat(
                _dwriteFactory,
                fontFamily,
                0,
                DWRITE_FONT_WEIGHT_EXTRA_BOLD,
                DWRITE_FONT_STYLE_NORMAL,
                DWRITE_FONT_STRETCH_NORMAL,
                fontSizePx,
                locale,
                out nint textFormat);

            var setTextAlign = (delegate* unmanaged[Stdcall]<nint, int, int>)(*(*(void***)textFormat + 3));
            setTextAlign(textFormat, 2);

            var setParaAlign = (delegate* unmanaged[Stdcall]<nint, int, int>)(*(*(void***)textFormat + 4));
            setParaAlign(textFormat, 2);

            return textFormat;
        }
    }

    private void DrawDigitMaskToSurface(CompositionDrawingSurface surface, int digit, int width, int height, nint textFormat)
    {
        Guid iidSurfaceInterop = new("2D6355C2-AD57-4EAE-92E4-4C3EFF65D578");
        nint pSurface = ((IWinRTObject)surface).NativeObject.ThisPtr;
        int qiHr = Marshal.QueryInterface(pSurface, in iidSurfaceInterop, out nint surfaceInterop);
        if (qiHr < 0 || surfaceInterop == 0)
            return;

        try
        {
            Guid iidD2DDeviceContext = new("e8f7fe7a-191c-466d-ad95-975678bda998");
            NativeMethods.POINT updateOffset;

            var beginDraw = (delegate* unmanaged[Stdcall]<nint, nint, in Guid, out nint, out NativeMethods.POINT, int>)(*(*(void***)surfaceInterop + 3));
            int hr = beginDraw(surfaceInterop, 0, in iidD2DDeviceContext, out nint d2dContext, out updateOffset);
            if (hr < 0 || d2dContext == 0) return;

            try
            {
                var clear = (delegate* unmanaged[Stdcall]<nint, in D2D1_COLOR_F, void>)(*(*(void***)d2dContext + 47));
                D2D1_COLOR_F transparent = default;
                clear(d2dContext, in transparent);

                var createSolidBrush = (delegate* unmanaged[Stdcall]<nint, in D2D1_COLOR_F, nint, out nint, int>)(*(*(void***)d2dContext + 8));
                D2D1_COLOR_F white = new() { r = 1f, g = 1f, b = 1f, a = 1f };
                createSolidBrush(d2dContext, in white, 0, out nint whiteBrush);

                try
                {
                    var drawText = (delegate* unmanaged[Stdcall]<
                        nint, char*, uint, nint, in D2D1_RECT_F, nint, int, int, void>)(*(*(void***)d2dContext + 27));

                    char ch = (char)('0' + digit);
                    D2D1_RECT_F layoutRect = new()
                    {
                        left = updateOffset.X,
                        top = updateOffset.Y - (6f * _dpiScale),
                        right = updateOffset.X + width,
                        bottom = updateOffset.Y + height - (6f * _dpiScale)
                    };

                    drawText(d2dContext, &ch, 1, textFormat, in layoutRect, whiteBrush, 0, 0);
                }
                finally
                {
                    if (whiteBrush != 0) Marshal.Release(whiteBrush);
                }
            }
            finally
            {
                var endDraw = (delegate* unmanaged[Stdcall]<nint, int>)(*(*(void***)surfaceInterop + 4));
                endDraw(surfaceInterop);
                Marshal.Release(d2dContext);
            }
        }
        finally
        {
            Marshal.Release(surfaceInterop);
        }
    }

    public void Dispose()
    {
        _graphicsDevice?.Dispose();
        if (_d2dDevice != 0) Marshal.Release(_d2dDevice);
        if (_dwriteFactory != 0) Marshal.Release(_dwriteFactory);
        if (_d2dFactory != 0) Marshal.Release(_d2dFactory);
    }

}
