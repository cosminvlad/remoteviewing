﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using RemoteViewing.ServerExample.ScreenCapture.Gdi;
using RemoteViewing.Vnc;
using SharpDX;
using SharpDX.Diagnostics;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
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

        /// <summary>
        ///   Creates a new Direct3D 11 video frame
        /// </summary>
        /// <param name="texture">Texture object</param>
        /// <param name="presentTime">Time, in 100-nanosecond units, of the frame capture</param>
        internal D3D11VideoFrame(Texture2D texture, OutputDuplicateMoveRectangle[] moveRectangles, RawRectangle[] dirtyRectangles )
        {
            Width = texture.Description.Width;
            Height = texture.Description.Height;

            Texture = texture;
            MoveRectangles = moveRectangles;
            DirtyRectangles = dirtyRectangles;
        }

        public override unsafe void CopyToVncFramebuffer(VncFramebuffer framebuffer)
        {
            using (var res = Texture.QueryInterface<SharpDX.Direct3D11.Resource>())
            {
                DataBox map = Texture.Device.ImmediateContext.MapSubresource(res, 0, MapMode.Read, MapFlags.None);

                fixed (byte* framebufferData = framebuffer.GetBuffer())
                {
                    //var data = (frame as GdiBitmapVideoFrame).BitmapData;
                    //VncPixelFormat.Copy(
                    //    data.Scan0,
                    //    data.Stride,
                    //    VncPixelFormat.RGB32,
                    //    new VncRectangle(0, 0, w, h),
                    //    (IntPtr)framebufferData,
                    //    framebuffer.Stride,
                    //    framebuffer.PixelFormat,
                    //    0,
                    //    0);

                    // merge partial captures into one big bitmap
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
        }
    }
}
