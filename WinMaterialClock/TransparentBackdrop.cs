using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WinMaterialClock;

internal sealed partial class TransparentBackdrop : SystemBackdrop
{
    private static nint s_dispatcherQueueController;
    private static Windows.UI.Composition.Compositor? s_sysCompositor;
    private static Windows.UI.Composition.CompositionColorBrush? s_transparentBrush;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        if (s_dispatcherQueueController == 0)
        {
            NativeMethods.DispatcherQueueOptions options = new()
            {
                dwSize = 12,
                threadType = 2,
                apartmentType = 2
            };
            NativeMethods.CreateDispatcherQueueController(options, out s_dispatcherQueueController);
        }

        s_sysCompositor ??= new Windows.UI.Composition.Compositor();
        s_transparentBrush ??= s_sysCompositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));
        connectedTarget.SystemBackdrop = s_transparentBrush;
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop connectedTarget)
    {
        connectedTarget.SystemBackdrop = null;
        base.OnTargetDisconnected(connectedTarget);
    }
}
