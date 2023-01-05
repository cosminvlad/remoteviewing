using System;
using SharpDX.Mathematics.Interop;

namespace RemoteViewing.ServerExample.ScreenCapture.Dxgi
{
    internal class PointerInfo
    {
        internal RawPoint PointerPosition { get; set; }

        public bool Visible { get; set; }

        public PointerImage Image { get; set; }

    }

    internal class PointerImage
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public int BytesPerPixel { get; set; }

        public byte[] Image { get; set; }

        public Span<byte> ImageSpan => Image.AsSpan(0, Width * Height * BytesPerPixel);
    }
}
