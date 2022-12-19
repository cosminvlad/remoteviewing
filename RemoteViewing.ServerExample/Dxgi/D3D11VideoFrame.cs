using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX.Direct3D11;

namespace RemoteViewing.ServerExample.Dxgi
{
    public class D3D11VideoFrame : VideoFrame
    {
        /// <summary>
        ///   Frame texture
        /// </summary>
        public Texture2D Texture { get; }

        /// <summary>
        ///   Creates a new Direct3D 11 video frame
        /// </summary>
        /// <param name="texture">Texture object</param>
        /// <param name="presentTime">Time, in 100-nanosecond units, of the frame capture</param>
        internal D3D11VideoFrame(Texture2D texture, long presentTime)
        {
            Width = texture.Description.Width;
            Height = texture.Description.Height;

            Texture = texture;
            PresentTime = presentTime;
        }
    }
}
