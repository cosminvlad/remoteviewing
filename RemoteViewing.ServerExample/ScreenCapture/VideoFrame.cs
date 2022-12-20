using RemoteViewing.Vnc;

namespace RemoteViewing.ServerExample.ScreenCapture
{
    public abstract class VideoFrame
    {
        /// <summary>
        ///   Number of pixels in a scanline
        /// </summary>
        public int Width { get; protected set; }

        /// <summary>
        ///   Number of scanlines
        /// </summary>
        public int Height { get; protected set; }

        public abstract void CopyToVncFramebuffer(VncFramebuffer framebuffer);
    }
}
