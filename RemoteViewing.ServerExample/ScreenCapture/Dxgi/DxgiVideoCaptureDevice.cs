﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;
using Bitmap = SharpDX.WIC.Bitmap;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Resource = SharpDX.DXGI.Resource;
using ResultCode = SharpDX.DXGI.ResultCode;

namespace RemoteViewing.ServerExample.ScreenCapture.Dxgi
{
    public sealed class DxgiVideoCaptureDevice : VideoCaptureDevice
    {
        /// <summary>
        ///   Timeout, in milliseconds, to consider a desktop duplication frame lost
        /// </summary>
        private const int DuplicationFrameTimeout = 1000;

        /// <summary>
        ///   Frequency of the system performance counter
        /// </summary>
        private readonly long perfFreq;

        /// <summary>
        ///   Array of capture sources
        /// </summary>
        private readonly DxgiCaptureSource[] sources;

        /// <summary>
        ///   Used-specified rectangle to be captured.
        /// </summary>
        /// <remarks>
        ///   Note that the coordinates for this rectangle represent a virtual desktop point and may be outside of the
        ///   primary monitor.
        /// </remarks>
        private readonly Rectangle virtualRect;

        private long lastMouseUpdateTime;
        private OutputDuplicateFrameInformation lastFrameInformation;

        /// <summary>
        ///   Enumerates the devices being used by the current capture sources
        /// </summary>
        public IEnumerable<SharpDX.DXGI.Device> Devices => this.sources.Select(s => s.DxgiDevice);

        /// <inheritdoc />
        /// <summary>
        ///   Class constructor
        /// </summary>
        /// <param name="x">Horizontal coordinate, in pixels, for the virtual capture location</param>
        /// <param name="y">Vertical coordinate, in pixels, for the virtual cpature location</param>
        /// <param name="width">Width, in pixels, for the captured region</param>
        /// <param name="height">Height, in pixels, for the captured region</param>
        public DxgiVideoCaptureDevice(int x, int y, int width, int height) : base(x, y, width, height)
        {
            this.virtualRect = new Rectangle(x, y, width, height);

            // obtain a list of capture sources
            using (var factory = new Factory1())
            {
                if (factory.GetAdapterCount1() == 0)
                {
                    throw new NotSupportedException("No suitable video adapters found");
                }

                var captureSources = new List<DxgiCaptureSource>();

                foreach (Adapter1 adapter in factory.Adapters1)
                {
                    Debug.WriteLine($"+ {adapter.Description1.Description}");

                    foreach (Output output in adapter.Outputs)
                    {
                        Debug.Write($"+   {output.Description.DeviceName} ");

                        int oWidth = output.Description.DesktopBounds.Right - output.Description.DesktopBounds.Left;
                        int oHeight = output.Description.DesktopBounds.Bottom - output.Description.DesktopBounds.Top;

                        var intersection = Rectangle.Intersect(this.virtualRect, new Rectangle(output.Description.DesktopBounds.Left, output.Description.DesktopBounds.Top, oWidth, oHeight));
                        if (intersection.Width > 0 && intersection.Height > 0)
                        {
                            try
                            {
                                Debug.WriteLine($"[XSECT: {intersection}]");
                                captureSources.Add(new DxgiCaptureSource(adapter,
                                                                         output,
                                                                         new Rectangle(intersection.X,
                                                                                       intersection.Y,
                                                                                       intersection.Width,
                                                                                       intersection.Height)));
                            }
                            catch (NotSupportedException exception)
                              when (exception.InnerException?.HResult == ResultCode.Unsupported.Result)
                            {
                                // HACK: when Captain itself is running on the dGPU, DDA calls fail with DXGI_ERROR_UNSPUPORTED
                                //       if the main desktop is not bound to it (see https://support.microsoft.com/en-us/kb/3019314).
                                //       We fix this by trying the next adapter
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[MISMATCH]");
                        }
                    }
                }

                this.sources = captureSources.ToArray();
            }
        }

        /// <inheritdoc />
        /// <summary>
        ///   Acquires a single frame
        /// </summary>
        /// <inheritdoc />
        /// <summary>
        ///   Acquires a single frame from this provider
        /// </summary>
        public override unsafe void AcquireFrame()
        {
            for (int i = 0; i < this.sources.Length; i++)
            {
                DxgiCaptureSource source = this.sources[i];

                try
                {
                    OutputDuplicateFrameInformation info;
                    Resource desktopResource = null;

                    do
                    {
                        // release previous frame if last capture attempt failed
                        if (desktopResource != null)
                        {
                            desktopResource.Dispose();
                            source.Duplication.ReleaseFrame();
                        }

                        // try to capture a frame
                        source.Duplication.TryAcquireNextFrame(DuplicationFrameTimeout,
                                                            out info,
                                                            out desktopResource);

                        this.lastFrameInformation = info;
                    } while (info.TotalMetadataBufferSize == 0);

                    GetFrameMoveRects(source);
                    GetFrameDirtyRects(source);

                    GetMousePointer(source, this.lastFrameInformation);

                    using (var srcResource = desktopResource.QueryInterface<SharpDX.Direct3D11.Resource>())
                    using (var destResource = source.Texture.QueryInterface<SharpDX.Direct3D11.Resource>())
                    {
                        // copy the entire screen region to the target texture
                        source.Device.ImmediateContext.CopySubresourceRegion(
                          srcResource,
                          0,
                          source.Subregion,
                          destResource,
                          0);
                    }

                    // release resources
                    desktopResource.Dispose();
                    source.Duplication.ReleaseFrame();
                }
                catch (SharpDXException exception) when (exception.ResultCode == ResultCode.AccessLost ||
                                                           exception.ResultCode == ResultCode.DeviceHung ||
                                                           exception.ResultCode == ResultCode.DeviceRemoved)
                {
                    // device has been lost - we can't ignore this and should try to reinitialize the D3D11 device until it's
                    // available again (...)
                    // we'll be receiving black/unsynced frames beyond this point - it is OK until we restore the device
                    this.sources[i].Alive = false;
                    while (!this.sources[i].Alive)
                    {
                        try
                        {
                            this.sources[i] = DxgiCaptureSource.Recreate(this.virtualRect);
                            this.sources[i].Alive = true;
                        }
                        catch (SharpDXException)
                        {
                            /* could not restore the capture source - keep trying */
                        }
                    }
                }
            }
        }

        private static unsafe void GetFrameDirtyRects(DxgiCaptureSource source)
        {
            int dirtyRectsBufferCount = 100;
            var dirtyRectsBuffer = new RawRectangle[dirtyRectsBufferCount];
            int dirtyRectsBufferSize = dirtyRectsBufferCount * sizeof(RawRectangle);
            int dirtyRectsBufferSizeRequired = 0;

            do
            {
                try
                {
                    source.Duplication.GetFrameDirtyRects(dirtyRectsBufferSize, dirtyRectsBuffer, out dirtyRectsBufferSizeRequired);
                    var returnedDirtyRectsBufferCount = dirtyRectsBufferSizeRequired / sizeof(RawRectangle);
                    //Console.WriteLine($"D {returnedDirtyRectsBufferCount:00} | {string.Join(" | ", dirtyRectsBuffer.Take(returnedDirtyRectsBufferCount).Select(r => $"{r.Left} {r.Top} {r.Right} {r.Bottom}"))}");
                    source.DirtyRectangles = dirtyRectsBuffer[..returnedDirtyRectsBufferCount];
                    break;
                }
                catch (SharpDXException ex) when (ex.ResultCode == DxgiError.DXGI_ERROR_MORE_DATA)
                {
                    dirtyRectsBufferCount += 100;
                    dirtyRectsBuffer = new RawRectangle[dirtyRectsBufferCount];
                    dirtyRectsBufferSize = dirtyRectsBufferCount * sizeof(RawRectangle);
                }
            } while (dirtyRectsBufferSizeRequired > dirtyRectsBufferSize);
        }

        private static unsafe void GetFrameMoveRects(DxgiCaptureSource source)
        {
            int moveRectsBufferCount = 100;
            var moveRectsBuffer = new OutputDuplicateMoveRectangle[moveRectsBufferCount];
            int moveRectsBufferSize = moveRectsBufferCount * sizeof(OutputDuplicateMoveRectangle);
            int moveRectsBufferSizeRequired = 0;

            do
            {
                try
                {
                    source.Duplication.GetFrameMoveRects(moveRectsBufferSize, moveRectsBuffer, out moveRectsBufferSizeRequired);
                    var returnedMoveRectsBufferCount = moveRectsBufferSizeRequired / sizeof(OutputDuplicateMoveRectangle);
                    //Console.WriteLine($"M {returnedMoveRectsBufferCount:00} | {string.Join(" | ", moveRectsBuffer.Take(returnedMoveRectsBufferCount).Select(r => $"{r.SourcePoint.X} {r.SourcePoint.Y} {r.DestinationRect.Left} {r.DestinationRect.Top}"))}");
                    source.MoveRectangles = moveRectsBuffer[..returnedMoveRectsBufferCount];
                    break;
                }
                catch (SharpDXException ex) when (ex.ResultCode == DxgiError.DXGI_ERROR_MORE_DATA)
                {
                    moveRectsBufferCount += 100;
                    moveRectsBuffer = new OutputDuplicateMoveRectangle[moveRectsBufferCount];
                    moveRectsBufferSize = moveRectsBufferCount * sizeof(OutputDuplicateMoveRectangle);
                }
            } while (moveRectsBufferSizeRequired > moveRectsBufferSize);
        }

        private unsafe void GetMousePointer(
            DxgiCaptureSource source,
            OutputDuplicateFrameInformation frameInfo)
        {
            if (frameInfo.LastMouseUpdateTime > this.lastMouseUpdateTime)
            {
                var pointerInfo = new PointerInfo
                {
                    PointerPosition = frameInfo.PointerPosition.Position,
                    Visible = frameInfo.PointerPosition.Visible,
                };
                source.PointerInfo = pointerInfo;
                this.lastMouseUpdateTime = frameInfo.LastMouseUpdateTime;
            }
            else
            {
                // Pointer was not updated, don't capture pointer info
                source.PointerInfo = null;
                return;
            }

            if (frameInfo.PointerShapeBufferSize != 0)
            {
                int poinerShapeBufferSize = frameInfo.PointerShapeBufferSize;
                var pointerShapeBuffer = new byte[poinerShapeBufferSize];
                int poinerShapeBufferSizeRequired = 0;

                fixed (byte* pointerShapeBufferRef = pointerShapeBuffer)
                {
                    source.Duplication.GetFramePointerShape(
                        poinerShapeBufferSize,
                        (IntPtr)pointerShapeBufferRef,
                        out poinerShapeBufferSizeRequired,
                        out var pointerShapeInformation);

                    if (pointerShapeInformation.Type == 1 /* DXGI_OUTDUPL_POINTER_SHAPE_TYPE_MONOCHROME */)
                    {

                        source.PointerInfo.Image = new PointerImage
                        {
                            Width = pointerShapeInformation.Width,
                            Height = pointerShapeInformation.Height,
                            BytesPerPixel = pointerShapeInformation.Pitch,
                            Image = pointerShapeBuffer[..poinerShapeBufferSizeRequired],
                        };
                    }
                    else if (pointerShapeInformation.Type == 2 /* DXGI_OUTDUPL_POINTER_SHAPE_TYPE_COLOR */
                        || pointerShapeInformation.Type == 4 /* DXGI_OUTDUPL_POINTER_SHAPE_TYPE_MASKED_COLOR */)
                    {
                        // https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/ne-dxgi1_2-dxgi_outdupl_pointer_shape_type
                        int w = pointerShapeInformation.Width, h = pointerShapeInformation.Height;

                        var cursorPixels = pointerShapeBuffer[..poinerShapeBufferSizeRequired];
                        var bitmask = new byte[(int)Math.Floor((w + 7) / 8.0) * h];

                        int bytesPerPixel = 4;
                        for (int i = 0; i < w; i++)
                        {
                            for (int j = 0; j < h; j++)
                            {
                                if (pointerShapeBuffer[j * w * bytesPerPixel + i * bytesPerPixel + 3] == 0)
                                {
                                    var bitIndex = j * w + i;
                                    int byteIndex = bitIndex / 8;
                                    int bitInByteIndex = bitIndex % 8;
                                    byte mask = (byte)(1 << bitInByteIndex);
                                    // Set the bit to 1
                                    bitmask[byteIndex] |= mask;
                                }
                            }
                        }

                        var contents = cursorPixels.Concat(bitmask).ToArray();

                        source.PointerInfo.Image = new PointerImage
                        {
                            Width = pointerShapeInformation.Width,
                            Height = pointerShapeInformation.Height,
                            BytesPerPixel = pointerShapeInformation.Pitch,
                            Image = contents,
                        };
                    }
                }
            }
            else
            {
                source.PointerInfo.Image = null;
            }
        }

        /// <inheritdoc />
        /// <summary>
        ///   Creates a single bitmap from the captured frames and returns an object with its information
        /// </summary>
        /// <returns>
        ///   A <see cref="VideoFrame" /> instance that can be either a <see cref="D3D11VideoFrame"/> or
        ///   a <see cref="BitmapVideoFrame" />
        /// </returns>
        public override VideoFrame LockFrame()
        {
            if (this.sources.Length == 1 && this.sources.First().Alive)
            {
                // TODO: if multiple textures are owned by a single adapter, merge them using CopySubresourceRegion
                return new D3D11VideoFrame(this.sources[0].Texture, this.sources[0].MoveRectangles, this.sources[0].DirtyRectangles, this.sources[0].PointerInfo);
            }

            return null;

            //using (var factory = new ImagingFactory2())
            //{
            //    // create Bitmap but DON'T dispose it here
            //    var bmp = new Bitmap(factory,
            //        this.virtualRect.Width,
            //        this.virtualRect.Height,
            //        PixelFormat.Format32bppBGRA,
            //        BitmapCreateCacheOption.CacheOnDemand);

            //    // caller is responsible for disposing BitmapLock
            //    BitmapLock data = bmp.Lock(BitmapLockFlags.Write);
            //    int minX = this.sources.Select(s => s.Region.Left).Min();
            //    int minY = this.sources.Select(s => s.Region.Top).Min();

            //    // map textures
            //    foreach (DxgiCaptureSource source in this.sources)
            //    {
            //        using (var res = source.Texture.QueryInterface<SharpDX.Direct3D11.Resource>())
            //        {
            //            DataBox map = source.Device.ImmediateContext.MapSubresource(res, 0, MapMode.Read, MapFlags.None);

            //            // merge partial captures into one big bitmap
            //            IntPtr dstScan0 = data.Data.DataPointer,
            //                   srcScan0 = map.DataPointer;
            //            int dstStride = data.Stride,
            //                srcStride = map.RowPitch;
            //            int srcWidth = source.Region.Right - source.Region.Left,
            //                srcHeight = source.Region.Bottom - source.Region.Top;
            //            int dstPixelSize = dstStride / data.Size.Width,
            //                srcPixelSize = srcStride / srcWidth;
            //            int dstX = source.Region.Left - minX,
            //                dstY = source.Region.Top - minY;

            //            for (int y = 0; y < srcHeight; y++)
            //            {
            //                Utilities.CopyMemory(IntPtr.Add(dstScan0, dstPixelSize * dstX + (y + dstY) * dstStride),
            //                                     IntPtr.Add(srcScan0, y * srcStride), srcPixelSize * srcWidth);
            //            }

            //            // release system memory
            //            source.Device.ImmediateContext.UnmapSubresource(res, 0);
            //        }
            //    }

            //    // return locked bitmap frame
            //    return new BitmapVideoFrame(bmp, data, presentTimeTicks);
            //}
        }

        /// <inheritdoc />
        /// <summary>
        ///   Releases the bitmap that may have been created for this frame
        /// </summary>
        /// <param name="frame">
        ///   Frame data structure returned by the <see cref="LockFrame" /> method
        /// </param>
        public override void UnlockFrame(VideoFrame frame)
        {
            //switch (frame)
            //{
            //    case D3D11VideoFrame bitmapFrame:
            //        bitmapFrame.Texture.Dispose();
            //        break;
            //}
        }

        /// <inheritdoc />
        /// <summary>
        ///   Releases resources used by the video provider
        /// </summary>
        public override void Dispose()
        {
            foreach (DxgiCaptureSource source in this.sources)
            {
                source.Dispose();
            }

            base.Dispose();
        }
    }
}
