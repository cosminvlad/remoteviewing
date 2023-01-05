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

            if (incremental)
            {
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

                if (dxgiFb.PointerInfo != null)
                {
                    session.FramebufferSendCursor(
                        dxgiFb.PointerInfo.PointerPosition.X,
                        dxgiFb.PointerInfo.PointerPosition.Y,
                        dxgiFb.PointerInfo.Image.Width,
                        dxgiFb.PointerInfo.Image.Height,
                        dxgiFb.PointerInfo.Image.Image);
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
