using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace USBWatcher.WinUI;

// Keeps cursor handling local to the grip, including while Thumb captures the pointer.
public sealed class ColumnResizeSurface : Grid
{
    public ColumnResizeSurface()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }
}
