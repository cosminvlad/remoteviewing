using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RemoteViewing.Vnc;

namespace RemoteViewing.ServerExample.ScreenCapture.Gdi
{
    public class GdiBitmapVideoFrame : VideoFrame
    {
        /// <summary>
        ///   GDI bitmap instance
        /// </summary>
        public Bitmap Bitmap { get; }

        /// <summary>
        ///   GDI bitmap lock object
        /// </summary>
        public BitmapData BitmapData { get; }

        /// <summary>
        ///   Creates a new video frame from a locked GDI bitmap
        /// </summary>
        /// <param name="bitmap">GDI bitmap</param>
        /// <param name="bitmapLock">Bitmap lock object</param>

        internal GdiBitmapVideoFrame(Bitmap bitmap, BitmapData bitmapLock)
        {
            Width = bitmap.Size.Width;
            Height = bitmap.Size.Height;

            Bitmap = bitmap;
            BitmapData = bitmapLock;
        }

        public override unsafe void CopyToVncFramebuffer(VncFramebuffer framebuffer)
        {
            fixed (byte* framebufferData = framebuffer.GetBuffer())
            {
                var data = BitmapData;
                VncPixelFormat.Copy(
                    data.Scan0,
                    data.Stride,
                    VncPixelFormat.RGB32,
                    new VncRectangle(0, 0, Width, Height),
                    (IntPtr)framebufferData,
                    framebuffer.Stride,
                    framebuffer.PixelFormat,
                    0,
                    0);
            }
        }
    }
}
