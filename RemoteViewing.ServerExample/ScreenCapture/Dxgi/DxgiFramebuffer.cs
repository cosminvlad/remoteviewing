using RemoteViewing.Vnc;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;

namespace RemoteViewing.ServerExample.ScreenCapture.Dxgi
{
    public class DxgiFramebuffer : VncFramebuffer
    {
        internal OutputDuplicateMoveRectangle[] MoveRectangles { get; set; }

        internal RawRectangle[] DirtyRectangles { get; set; }

        internal PointerInfo PointerInfo { get; set; }

        public DxgiFramebuffer(string name, int width, int height, VncPixelFormat pixelFormat)
            : base(name, width, height, pixelFormat)
        {
        }
    }
}
