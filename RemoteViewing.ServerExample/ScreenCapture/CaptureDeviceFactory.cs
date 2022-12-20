using RemoteViewing.ServerExample.ScreenCapture.Dxgi;
using RemoteViewing.ServerExample.ScreenCapture.Gdi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteViewing.ServerExample.ScreenCapture
{
    public static class CaptureDeviceFactory
    {
        /// <summary>
        ///   Picks the most suitable capture device for the current platform and environment and creates an instance of it.
        /// </summary>
        /// <param name="x">X coordinate for the region being captured</param>
        /// <param name="y">Y coordinate for the region being captured</param>
        /// <param name="width">Width of the region being captured</param>
        /// <param name="height">Height of the region being captured</param>
        /// <returns>A <see cref="VideoCaptureDevice" /> instance</returns>
        public static VideoCaptureDevice CreateVideoCaptureDevice(int x, int y, int width, int height)
        {
            if (Environment.OSVersion.Version >= new Version(6, 2))
            {
                try
                {
                    return new DxgiVideoCaptureDevice(x, y, width, height);
                }
                catch (NotSupportedException) { }
            }

            return new GdiVideoCaptureDevice(x, y, width, height);
        }
    }
}
