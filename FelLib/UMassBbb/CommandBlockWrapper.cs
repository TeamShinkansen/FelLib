using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace FelLib.UMassBbb
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommandBlockWrapper
    {
        public const uint SIGNATURE = 0x43425355;

        public uint dCBWSignature;
        public uint dCBWTag;
        public uint dCBWDataTransferLength;
        public byte bmCBWFlags;
        public byte bCBWLUN;
        public byte bCBWCBLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] CBWCB;

        public static CommandBlockWrapper Create(bool isIn)
        {
            CommandBlockWrapper cbw = new CommandBlockWrapper();
            cbw.dCBWSignature = SIGNATURE;
            if (isIn) cbw.bmCBWFlags |= 0x80;
            cbw.CBWCB = new byte[16];
            return cbw;
        }
    }
}
