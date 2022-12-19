using RemoteViewing.Vnc;
using SharpDX;
using Silk.NET.Core.Native;
using Silk.NET.DXGI;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RemoteViewing.ServerExample.Dxgi
{
    internal class DxgiFramebufferSource : IVncFramebufferSource
    {
        public bool SupportsResizing { get; }

        public unsafe VncFramebuffer Capture()
        {
            unsafe
            {
                using var api = DXGI.GetApi();

                IDXGIFactory1* factory1 = default;

                try
                {
                    SilkMarshal.ThrowHResult(api.CreateDXGIFactory1(SilkMarshal.GuidPtrOf<IDXGIFactory1>(), (void**)&factory1));


                    uint i = 0u;
                    while (true)
                    {
                        IDXGIAdapter1* adapter1 = default;
                        var res = factory1->EnumAdapters1(i, &adapter1);

                        var exception = Marshal.GetExceptionForHR(res);
                        if (exception != null) break;

                        AdapterDesc1 adapterDesc = default;
                        SilkMarshal.ThrowHResult(adapter1->GetDesc1(&adapterDesc));

                        var systemMemory = (ulong)adapterDesc.DedicatedSystemMemory;
                        var videoMemory = (ulong)adapterDesc.DedicatedVideoMemory;

                        uint j = 0u;
                        while (true)
                        {
                            IDXGIOutput* output = default;
                            var resOutput = adapter1->EnumOutputs(j, &output);

                            var exceptionOutput = Marshal.GetExceptionForHR(resOutput);
                            if (exceptionOutput != null) break;


                            OutputDesc outputDesc = default;
                            SilkMarshal.ThrowHResult(output->GetDesc(&outputDesc));

                            IDXGIOutput6* output6 = default;
                            SilkMarshal.ThrowHResult(output->QueryInterface(SilkMarshal.GuidPtrOf<IDXGIOutput6>(), (void**)&output6));

                            output6->DuplicateOutput1()

                            j++;
                        }

                        adapter1->Release();
                        i++;
                    }
                }
                finally
                {

                    if (factory1->LpVtbl != (void**)IntPtr.Zero)
                        factory1->Release();
                }
            }
        }

        public ExtendedDesktopSizeStatus SetDesktopSize(int width, int height)
        {
            throw new NotImplementedException();
        }
    }
}
