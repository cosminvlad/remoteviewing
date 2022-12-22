using RemoteViewing.ServerExample.Native;
using RemoteViewing.ServerExample.ScreenCapture;
using RemoteViewing.Vnc;
using System;
using System.Drawing;
using RemoteViewing.ServerExample.ScreenCapture.Dxgi;

namespace RemoteViewing.ServerExample
{

    /// <summary>
    /// Provides a framebuffer with pixels copied from the screen.
    /// </summary>
    public partial class ScreenFramebufferSource : IVncFramebufferSource
    {
        private Bitmap bitmap;
        private VncFramebuffer framebuffer;
        private string name;
        private readonly VideoCaptureDevice captureDevice;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScreenFramebufferSource"/> class.
        /// </summary>
        /// <param name="name">The framebuffer name. Many VNC clients set their titlebar to this name.</param>
        /// <param name="screen">The bounds of the screen region.</param>
        public ScreenFramebufferSource(string name)
        {

            this.name = name ?? throw new ArgumentNullException(nameof(name));

            var windowHandle = User32.GetDesktopWindow();
            User32.RECT windowRect = new User32.RECT();
            User32.GetWindowRect(windowHandle, ref windowRect);
            int width = windowRect.right - windowRect.left;
            int height = windowRect.bottom - windowRect.top;
            this.captureDevice = CaptureDeviceFactory.CreateVideoCaptureDevice(windowRect.left, windowRect.top, width, height);
        }

        /// <inheritdoc/>
        public bool SupportsResizing => false;

        /// <inheritdoc/>
        public ExtendedDesktopSizeStatus SetDesktopSize(int width, int height)
        {
            return ExtendedDesktopSizeStatus.Prohibited;
        }

        /// <summary>
        /// Captures the screen.
        /// </summary>
        /// <returns>A framebuffer corresponding to the screen.</returns>
        public unsafe VncFramebuffer Capture()
        {
            VideoFrame frame = default;
            captureDevice.AcquireFrame();
            try
            {
                frame = captureDevice.LockFrame();

                int w = frame.Width, h = frame.Height;

                if (this.bitmap == null || this.bitmap.Width != w || this.bitmap.Height != h)
                {
                    this.bitmap = new Bitmap(w, h);
                    this.framebuffer = new DxgiFramebuffer(this.name, w, h, new VncPixelFormat());
                }

                lock (this.framebuffer.SyncRoot)
                {
                    //fixed (byte* framebufferData = framebuffer.GetBuffer())
                    //{
                    //    var data = (frame as GdiBitmapVideoFrame).BitmapData;
                    //    VncPixelFormat.Copy(
                    //        data.Scan0,
                    //        data.Stride,
                    //        VncPixelFormat.RGB32,
                    //        new VncRectangle(0, 0, w, h),
                    //        (IntPtr)framebufferData,
                    //        framebuffer.Stride,
                    //        framebuffer.PixelFormat,
                    //        0,
                    //        0);
                    //}

                    frame.CopyToVncFramebuffer(framebuffer);
                }
            }
            finally
            {
                if (frame != null)
                {
                    captureDevice.UnlockFrame(frame);
                }
            }

            return this.framebuffer;
        }
    }
}
