using Microsoft.Extensions.Logging;
using RemoteViewing.Vnc;
using RemoteViewing.Vnc.Server;
using System;

namespace RemoteViewing.ServerExample.ScreenCapture.Dxgi
{
    /// <summary>
    /// Caches the <see cref="VncFramebuffer"/> pixel data and updates them as new
    /// <see cref="VncFramebuffer"/> commands are received.
    /// </summary>
    internal sealed class DxgiFramebufferCache : IVncFramebufferCache
    {
        // The size of the tiles which will be invalidated. So we're basically
        // dividing the framebuffer in blocks of 32x32 and are invalidating them one at a time.
        private const int TileSize = 64;

        private readonly ILogger logger;

        private readonly bool[] isLineInvalid;

        // We cache the latest framebuffer data as it was sent to the client. When looking for changes,
        // we compare with the framebuffer which is cached here and send the deltas (for each time
        // which was invalidate) to the client.
        private VncFramebuffer cachedFramebuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DxgiFramebufferCache"/> class.
        /// </summary>
        /// <param name="framebuffer">
        /// The <see cref="VncFramebuffer"/> to cache.
        /// </param>
        /// <param name="logger">
        /// The <see cref="ILogger"/> logger to use when logging diagnostic messages.
        /// </param>
        public DxgiFramebufferCache(VncFramebuffer framebuffer, ILogger logger)
        {
            if (framebuffer == null)
            {
                throw new ArgumentNullException(nameof(framebuffer));
            }

            this.Framebuffer = framebuffer;
            this.cachedFramebuffer = new VncFramebuffer(framebuffer.Name, framebuffer.Width, framebuffer.Height, framebuffer.PixelFormat);

            this.logger = logger;
            this.isLineInvalid = new bool[this.Framebuffer.Height];
        }

        /// <summary>
        /// Gets an up-to-date and complete <see cref="VncFramebuffer"/>.
        /// </summary>
        public VncFramebuffer Framebuffer
        {
            get;
            private set;
        }

        /// <summary>
        /// Responds to a <see cref="VncServerSession"/> update request.
        /// </summary>
        /// <param name="session">
        /// The session on which the update request was received.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the operation completed successfully; otherwise,
        /// <see langword="false"/>.
        /// </returns>
        public unsafe bool RespondToUpdateRequest(IVncServerSession session)
        {
            VncRectangle subregion = default(VncRectangle);

            var fb = this.Framebuffer;
            var fbr = session.FramebufferUpdateRequest;
            if (fb == null || fbr == null)
            {
                return false;
            }

            var incremental = fbr.Incremental;
            var region = fbr.Region;
            int bpp = fb.PixelFormat.BytesPerPixel;

            this.logger?.LogDebug($"Responding to an update request for region {region}.");

            session.FramebufferManualBeginUpdate();

            // Take a lock here, as we will modify
            // both buffers heavily in the next block.
            //lock (fb.SyncRoot)
            //{
            //    lock (this.cachedFramebuffer.SyncRoot)
            //    {
            //        var actualBuffer = this.Framebuffer.GetBuffer();
            //        var bufferedBuffer = this.cachedFramebuffer.GetBuffer();

            //        // In this block, we will determine which rectangles need updating. Right now, we consider
            //        // each line at once. It's not a very efficient algorithm, but it works.
            //        // We're going to start at the upper-left position of the region, and then we will work our way down,
            //        // on a line by line basis, to determine if each line is still valid.
            //        // isLineInvalid will indicate, on a line-by-line basis, whether a line is still valid or not.
            //        for (int y = region.Y; y < region.Y + region.Height; y++)
            //        {
            //            subregion.X = region.X;
            //            subregion.Y = y;
            //            subregion.Width = region.Width;
            //            subregion.Height = 1;

            //            // For a given y, the x pixels are stored sequentially in the array
            //            // starting at y * stride (number of bytes per row); for each x
            //            // value there are bpp bytes of data (4 for a 32-bit integer); we are looking
            //            // for pixels between x and x + w so this translates to
            //            // y * stride + bpp * x and y * stride + bpp * (x + w)
            //            int srcOffset = (y * this.Framebuffer.Stride) + (bpp * region.X);
            //            int length = bpp * region.Width;

            //            var isValid = actualBuffer.AsSpan().Slice(srcOffset, length)
            //                              .SequenceCompareTo(bufferedBuffer.AsSpan().Slice(srcOffset, length)) == 0;

            //            if (!isValid)
            //            {
            //                try
            //                {
            //                    Buffer.BlockCopy(actualBuffer, srcOffset, bufferedBuffer, srcOffset, length);
            //                }
            //                catch
            //                {
            //                    throw;
            //                }
            //            }

            //            this.isLineInvalid[y - region.Y] = !isValid;
            //        }
            //    } // lock
            //} // lock

            if (incremental)
            {
                //// Determine logical group of lines which are invalid. We find the first line which is invalid,
                //// create a new region which contains the all invalid lines which immediately follow the current line.
                //// If we find a valid line, we'll create a new region.
                //int? y = null;

                //for (int line = 0; line < region.Height; line++)
                //{
                //    if (y == null && this.isLineInvalid[line])
                //    {
                //        y = region.Y + line;
                //    }

                //    if (y != null && (!this.isLineInvalid[line] || line == region.Height - 1))
                //    {
                //        // Flush
                //        subregion.X = region.X;
                //        subregion.Y = region.Y + y.Value;
                //        subregion.Width = region.Width;
                //        subregion.Height = line - y.Value + 1;
                //        session.FramebufferManualInvalidate(subregion);
                //        y = null;
                //    }
                //}



                var dxgiFb = fb as DxgiFramebuffer;

                foreach (var moveRectangle in dxgiFb.MoveRectangles)
                {
                    session.FramebufferManualCopyRegion(new VncRectangle(
                            moveRectangle.DestinationRect.Left,
                            moveRectangle.DestinationRect.Top,
                            moveRectangle.DestinationRect.Right - moveRectangle.DestinationRect.Left,
                            moveRectangle.DestinationRect.Bottom - moveRectangle.DestinationRect.Top),
                        moveRectangle.SourcePoint.X, moveRectangle.SourcePoint.Y);
                }

                foreach (var dirtyRectangle in dxgiFb.DirtyRectangles)
                {
                    session.FramebufferManualInvalidate(new VncRectangle(
                            dirtyRectangle.Left,
                            dirtyRectangle.Top,
                            dirtyRectangle.Right - dirtyRectangle.Left,
                            dirtyRectangle.Bottom - dirtyRectangle.Top));
                }
            }
            else
            {
                session.FramebufferManualInvalidate(region);
            }

            return session.FramebufferManualEndUpdate();
        }
    }
}
