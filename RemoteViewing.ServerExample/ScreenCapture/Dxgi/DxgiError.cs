using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteViewing.ServerExample.ScreenCapture.Dxgi
{
    public static class DxgiError
    {
        public const uint E_ACCESS_DENIED = 0x80070005;
        public const uint DXGI_ERROR_ACCESS_LOST = 0x887A0026;
        public const uint DXGI_ERROR_WAIT_TIMEOUT = 0x887A0027;
        public const uint DXGI_ERROR_MORE_DATA = 0x887A0003;
    }
}
