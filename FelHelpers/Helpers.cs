using FelLib;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace FelHelpers
{
    public class Helpers : IDisposable
    {
        byte[] fes { get; set; }
        byte[] uboot { get; set; }

        public Helpers(byte[] fes, byte[] uboot)
        {
            this.fes = fes;
            this.uboot = uboot;
        }

        public Helpers(Stream fes, Stream uboot, bool closeStreams = false)
        {
            this.fes = StreamHelpers.ReadToEnd(fes);
            if (closeStreams)
                fes.Close();

            this.uboot = StreamHelpers.ReadToEnd(uboot);
            if (closeStreams)
               uboot.Close();
        }

        public static void WaitForDevice()
        {
            while (!Fel.DeviceExists())
            {
                Thread.Sleep(100);
            }
        }

        
        public void Memboot(byte[] bootImage) => Memboot(fes, uboot, bootImage);
        public static void Memboot(Stream fes, Stream uboot, Stream bootImage, bool closeStreams = false)
        {
            Memboot(StreamHelpers.ReadToEnd(fes), StreamHelpers.ReadToEnd(uboot), StreamHelpers.ReadToEnd(bootImage));
            if (closeStreams)
            {
                fes.Close();
                uboot.Close();
                bootImage.Close();
            }
        }
        public static void Memboot(byte[] fes, byte[] uboot, byte[] bootImage)
        {
            var fel = new Fel();
            fel.Fes1Bin = fes;
            fel.UBootBin = uboot;
            if (!fel.Open())
            {
                throw new Exception("USB Device Not Found");
            }

            var size = CalcKernelSize(bootImage);
            if (size > bootImage.Length || size > Fel.transfer_max_size)
                throw new Exception($"Invalid Kernel Size: {size}");
            size = (size + Fel.sector_size - 1) / Fel.sector_size;
            size = size * Fel.sector_size;
            if (bootImage.Length != size)
            {
                var newK = new byte[size];
                Array.Copy(bootImage, newK, bootImage.Length);
                bootImage = newK;
            }

            // upload kernel through fel
            fel.WriteMemory(Fel.transfer_base_m, bootImage,
                delegate (Fel.CurrentAction action, string command)
                {
                    switch (action)
                    {
                        case Fel.CurrentAction.WritingMemory:
                            break;
                    }
                }
            );

            var bootCommand = string.Format("boota {0:x}", Fel.transfer_base_m);
            fel.RunUbootCmd(bootCommand, true);
        }


        public void FlashBoot(byte[] bootImage) => FlashBoot(fes, uboot, bootImage);
        public static void FlashBoot(Stream fes, Stream uboot, Stream bootImage, bool closeStreams = false)
        {
            FlashBoot(StreamHelpers.ReadToEnd(fes), StreamHelpers.ReadToEnd(uboot), StreamHelpers.ReadToEnd(bootImage));
            if (closeStreams)
            {
                fes.Close();
                uboot.Close();
                bootImage.Close();
            }
        }
        public static void FlashBoot(byte[] fes, byte[] uboot, byte[] bootImage)
        {
            throw new NotImplementedException();
        }


        public void FlashUboot(byte[] uboot, bool flashAll = false) => FlashUboot(fes, uboot, flashAll);
        public static void FlashUboot(Stream fes, Stream uboot, bool flashAll = false, bool closeStreams = false)
        {
            FlashUboot(StreamHelpers.ReadToEnd(fes), StreamHelpers.ReadToEnd(uboot), flashAll);
            if (closeStreams)
            {
                fes.Close();
                uboot.Close();
            }
        }
        public static void FlashUboot(byte[] fes, byte[] uboot, bool flashAll = false)
        {
            throw new NotImplementedException();
        }

        public void WriteFlash(UInt32 address, byte[] data) => WriteFlash(fes, uboot, address, data);
        public static void WriteFlash(Stream fes, Stream uboot, UInt32 address, Stream data, bool closeStreams = false)
        {
            WriteFlash(StreamHelpers.ReadToEnd(fes), StreamHelpers.ReadToEnd(uboot), address, StreamHelpers.ReadToEnd(data));
            if (closeStreams)
            {
                fes.Close();
                uboot.Close();
                data.Close();
            }
        }
        public static void WriteFlash(byte[] fes, byte[] uboot, UInt32 address, byte[] data)
        {
            throw new NotImplementedException();
        }

        public byte[] ReadFlash(UInt32 address, UInt32 length) => ReadFlash(fes, uboot, address, length);
        public byte[] ReadFlash(Stream fes, Stream uboot, UInt32 address, UInt32 length, bool closeStreams = false)
        {
            var result = ReadFlash(StreamHelpers.ReadToEnd(fes), StreamHelpers.ReadToEnd(uboot), address, length);
            if (closeStreams)
            {
                fes.Close();
                uboot.Close();
            }
            return result;
        }
        public static byte[] ReadFlash(byte[] fes, byte[] uboot, UInt32 address, UInt32 length)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            fes = null;
            uboot = null;
        }

        static UInt32 CalcKernelSize(byte[] header)
        {
            if (Encoding.ASCII.GetString(header, 0, 8) != "ANDROID!") throw new Exception("Invalid Header");
            UInt32 kernel_size = (UInt32)(header[8] | (header[9] * 0x100) | (header[10] * 0x10000) | (header[11] * 0x1000000));
            UInt32 kernel_addr = (UInt32)(header[12] | (header[13] * 0x100) | (header[14] * 0x10000) | (header[15] * 0x1000000));
            UInt32 ramdisk_size = (UInt32)(header[16] | (header[17] * 0x100) | (header[18] * 0x10000) | (header[19] * 0x1000000));
            UInt32 ramdisk_addr = (UInt32)(header[20] | (header[21] * 0x100) | (header[22] * 0x10000) | (header[23] * 0x1000000));
            UInt32 second_size = (UInt32)(header[24] | (header[25] * 0x100) | (header[26] * 0x10000) | (header[27] * 0x1000000));
            UInt32 second_addr = (UInt32)(header[28] | (header[29] * 0x100) | (header[30] * 0x10000) | (header[31] * 0x1000000));
            UInt32 tags_addr = (UInt32)(header[32] | (header[33] * 0x100) | (header[34] * 0x10000) | (header[35] * 0x1000000));
            UInt32 page_size = (UInt32)(header[36] | (header[37] * 0x100) | (header[38] * 0x10000) | (header[39] * 0x1000000));
            UInt32 dt_size = (UInt32)(header[40] | (header[41] * 0x100) | (header[42] * 0x10000) | (header[43] * 0x1000000));
            UInt32 pages = 1;
            pages += (kernel_size + page_size - 1) / page_size;
            pages += (ramdisk_size + page_size - 1) / page_size;
            pages += (second_size + page_size - 1) / page_size;
            pages += (dt_size + page_size - 1) / page_size;
            return pages * page_size;
        }
    }
}
