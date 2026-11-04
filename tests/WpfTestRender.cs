using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// Synchronous STA tests do not run Application.Run. Unlike the production
// dispatcher loop, they must explicitly process pending resource/layout work
// between offscreen renders. Never call Drain from Measure/Arrange/OnRender.
internal static class WpfTestRender
{
    internal static void Drain()
    {
        // Background uses the dispatcher queue on the same thread; Send would
        // execute inline and leave earlier Render/Loaded/Input work pending.
        // https://learn.microsoft.com/dotnet/api/system.windows.threading.dispatcherpriority
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
    }

    internal static void Render(RenderTargetBitmap target, Visual visual)
    {
        Drain();
        target.Render(visual);
        // Render registers asynchronous WPF resource updates. Handle them here,
        // outside its DisableProcessing scope, before the next layout/render.
        // https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Media/Imaging/RenderTargetBitmap.cs
        Drain();
    }
}
