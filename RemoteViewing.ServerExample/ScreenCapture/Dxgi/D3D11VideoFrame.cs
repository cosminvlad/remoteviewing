using RemoteViewing.Vnc;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace RemoteViewing.ServerExample.ScreenCapture.Dxgi
{
    public class D3D11VideoFrame : VideoFrame
    {
        /// <summary>
        ///   Frame texture
        /// </summary>
        public Texture2D Texture { get; }

        internal OutputDuplicateMoveRectangle[] MoveRectangles { get; set; }

        internal RawRectangle[] DirtyRectangles { get; set; }

        internal PointerInfo PointerInfo { get; set; }

        /// <summary>
        ///   Creates a new Direct3D 11 video frame
        /// </summary>
        /// <param name="texture">Texture object</param>
        /// <param name="presentTime">Time, in 100-nanosecond units, of the frame capture</param>
        internal D3D11VideoFrame(Texture2D texture, OutputDuplicateMoveRectangle[] moveRectangles, RawRectangle[] dirtyRectangles, PointerInfo pointerInfo)
        {
            Width = texture.Description.Width;
            Height = texture.Description.Height;

            Texture = texture;
            MoveRectangles = moveRectangles;
            DirtyRectangles = dirtyRectangles;
            PointerInfo = pointerInfo;
        }

        public override unsafe void CopyToVncFramebuffer(VncFramebuffer framebuffer)
        {
            using (var res = Texture.QueryInterface<SharpDX.Direct3D11.Resource>())
            {
                DataBox map = Texture.Device.ImmediateContext.MapSubresource(res, 0, MapMode.Read, MapFlags.None);

                fixed (byte* framebufferData = framebuffer.GetBuffer())
                {
                    IntPtr dstScan0 = (IntPtr)framebufferData,
                        srcScan0 = map.DataPointer;
                    int dstStride = framebuffer.Stride,
                        srcStride = map.RowPitch;
                    int srcWidth = Width,
                        srcHeight = Height;
                    int dstPixelSize = dstStride / framebuffer.Width,
                        srcPixelSize = srcStride / srcWidth;
                    int dstX = 0,
                        dstY = 0;

                    for (int y = 0; y < Height; y++)
                    {
                        Utilities.CopyMemory(IntPtr.Add(dstScan0, dstPixelSize * dstX + (y + dstY) * dstStride),
                            IntPtr.Add(srcScan0, y * srcStride), srcPixelSize * srcWidth);
                    }
                }

                // release system memory
                Texture.Device.ImmediateContext.UnmapSubresource(res, 0);
            }

            var dxgiFramebuffer = framebuffer as DxgiFramebuffer;

            //DetectSolidRectangles(dxgiFramebuffer);

            dxgiFramebuffer.MoveRectangles = MoveRectangles;
            dxgiFramebuffer.DirtyRectangles = DirtyRectangles;
            dxgiFramebuffer.PointerInfo = PointerInfo;
        }

        private void DetectSolidRectangles(DxgiFramebuffer dxgiFramebuffer)
        {
            const int MIN_SOLID_RECTANGLE_SIZE = 16;
            foreach (var dirtyRectangle in DirtyRectangles)
            {
                for (int dy = dirtyRectangle.Top; dy < dirtyRectangle.Bottom; dy += MIN_SOLID_RECTANGLE_SIZE)
                {
                    for (int dx = dirtyRectangle.Left; dx < dirtyRectangle.Right; dx += MIN_SOLID_RECTANGLE_SIZE)
                    {

                    }
                }
            }
        }
    }
}
