using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace FelLib.UMassBbb
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommandStatusWrapper
    {
        public const uint SIGNATURE = 0x53425355;

        public uint dCSWSignature;
        public uint dCSWTag;
        public uint dCSWDataResidue;
        public byte bCSWStatus;
    }
}
