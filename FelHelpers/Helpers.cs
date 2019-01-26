using FelLib;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace FelHelpers
{
    public class Helpers : IDisposable
    {
        public event Fel.WriteLineHandler WriteLine;

        public delegate void SetStatusHandler(string message);
        public event SetStatusHandler SetStatus;

        public delegate void SetProgressHandler(long progress, long max);
        public event SetProgressHandler SetProgress;

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

        public Helpers() { }

        public static void WaitForDevice()
        {
            while (!Fel.DeviceExists())
            {
                Thread.Sleep(100);
            }
        }

        public void Memboot(byte[] bootImage) => Memboot(fes, uboot, bootImage);
        public void Memboot(Stream fes, Stream uboot, Stream bootImage, bool closeStreams = false)
        {
            Memboot(StreamHelpers.ReadToEnd(fes), StreamHelpers.ReadToEnd(uboot), StreamHelpers.ReadToEnd(bootImage));
            if (closeStreams)
            {
                fes.Close();
                uboot.Close();
                bootImage.Close();
            }
        }
        public void Memboot(byte[] fes, byte[] uboot, byte[] bootImage, Fel.WriteLineHandler writeLineHandler = null)
        {
            var fel = new Fel();
            fel.WriteLine += WriteLine;
            fel.Fes1Bin = fes;
            fel.UBootBin = uboot;
            if (!fel.Open())
            {
                throw new Exception("USB Device Not Found");
            }

            var size = CalcKernelSize(bootImage);
            if (size > bootImage.Length || size > Fel.transfer_max_size)
                throw new Exception($"Invalid boot image size: {size}");
            size = (size + Fel.sector_size - 1) / Fel.sector_size;
            size = size * Fel.sector_size;
            if (bootImage.Length != size)
            {
                var newK = new byte[size];
                Array.Copy(bootImage, newK, bootImage.Length);
                bootImage = newK;
            }

            long maxProgress = size / 65536, progress = 0;

            // upload kernel through fel
            fel.WriteMemory(Fel.transfer_base_m, bootImage,
                delegate (Fel.CurrentAction action, string command)
                {
                    switch (action)
                    {
                        case Fel.CurrentAction.WritingMemory:
                            SetStatus?.Invoke("Uploading boot image");
                            break;
                    }
                    progress++;
                    SetProgress?.Invoke(Math.Min(progress, maxProgress), maxProgress);
                }
            );

            var bootCommand = string.Format("boota {0:x}", Fel.transfer_base_m);
            RunCommand(fel, bootCommand, true);
        }

        public void FlashBoot(byte[] bootImage) => FlashBoot(fes, uboot, bootImage);
        public void FlashBoot(Stream fes, Stream uboot, Stream bootImage, bool closeStreams = false)
        {
            FlashBoot(StreamHelpers.ReadToEnd(fes), StreamHelpers.ReadToEnd(uboot), StreamHelpers.ReadToEnd(bootImage));
            if (closeStreams)
            {
                fes.Close();
                uboot.Close();
                bootImage.Close();
            }
        }
        public void FlashBoot(byte[] fes, byte[] uboot, byte[] bootImage)
        {
            var fel = new Fel();
            fel.WriteLine += WriteLine;
            fel.Fes1Bin = fes;
            fel.UBootBin = uboot;
            if (!fel.Open())
            {
                throw new Exception("USB Device Not Found");
            }

            var size = CalcKernelSize(bootImage);
            if (size > bootImage.Length || size > Fel.kernel_max_size)
                throw new Exception($"Invalid boot image size: {size}");

            WriteFlash(fel, fes, uboot, Fel.kernel_base_f, bootImage, true, "boot image");

            RunCommand(fel, "shutdown", true);
            SetProgress(1, 1);
        }


        public void FlashUboot(byte[] uboot, bool flashAll = false) => FlashUboot(fes, uboot, flashAll);
        public void FlashUboot(Stream fes, Stream uboot, bool flashAll = false, bool closeStreams = false)
        {
            FlashUboot(StreamHelpers.ReadToEnd(fes), StreamHelpers.ReadToEnd(uboot), flashAll);
            if (closeStreams)
            {
                fes.Close();
                uboot.Close();
            }
        }
        public void FlashUboot(byte[] fes, byte[] uboot, bool flashAll = false)
        {
            var fel = new Fel();
            fel.WriteLine += WriteLine;
            fel.Fes1Bin = fes;
            fel.UBootBin = uboot;
            if (!fel.Open())
            {
                throw new Exception("USB Device Not Found");
            }

            if(uboot.Length > Fel.uboot_maxsize_f)
                throw new Exception($"Invalid uboot size: {uboot.Length}");

            WriteFlash(fel, fes, uboot, Fel.uboot_base_f, uboot, true, "u-boot");

            RunCommand(fel, "shutdown", true);
            SetProgress(1, 1);
        }

        public void WriteFlash(UInt32 address, byte[] data, bool verify = true, string what = "NAND") => WriteFlash(fes, uboot, address, data, verify, what);
        public void WriteFlash(Stream fes, Stream uboot, UInt32 address, Stream data, bool closeStreams = false, bool verify = true, string what = "NAND")
        {
            WriteFlash(StreamHelpers.ReadToEnd(fes), StreamHelpers.ReadToEnd(uboot), address, StreamHelpers.ReadToEnd(data), verify, what);
            if (closeStreams)
            {
                fes.Close();
                uboot.Close();
                data.Close();
            }
        }
        public void WriteFlash(byte[] fes, byte[] uboot, UInt32 address, byte[] data, bool verify = true, string what = "NAND")
        {
            var fel = new Fel();
            fel.WriteLine += WriteLine;
            fel.Fes1Bin = fes;
            fel.UBootBin = uboot;
            if (!fel.Open())
            {
                throw new Exception("USB Device Not Found");
            }

            WriteFlash(fel, fes, uboot, address, data, verify, what);
        }
        public void WriteFlash(Fel fel, byte[] fes, byte[] uboot, UInt32 address, byte[] data, bool verify = true, string what = "NAND")
        {
            var size = data.LongLength;
            size = (size + Fel.sector_size - 1) / Fel.sector_size;
            size = size * Fel.sector_size;
            if (data.LongLength != size)
            {
                var newData = new byte[size];
                Array.Copy(data, newData, data.Length);
                data = newData;
            }


            long maxProgress = (size / 65536) * (verify ? 2 : 1), progress = 0;

            // upload kernel through fel
            fel.WriteFlash(address, data,
                delegate (Fel.CurrentAction action, string command)
                {
                    switch (action)
                    {
                        case Fel.CurrentAction.WritingMemory:
                            SetStatus?.Invoke("Writing " + what);
                            break;
                    }
                    progress++;
                    SetProgress?.Invoke(Math.Min(progress, maxProgress), maxProgress);
                }
            );

            if (verify)
            {


                var r = fel.ReadFlash((UInt32)Fel.kernel_base_f, (UInt32)data.LongLength,
                    delegate (Fel.CurrentAction action, string command)
                    {
                        switch (action)
                        {
                            case Fel.CurrentAction.ReadingMemory:
                                SetStatus("Reading " + what);
                                break;
                        }
                        progress++;
                        SetProgress?.Invoke(Math.Min(progress, maxProgress), maxProgress);
                    }
                );

                if (!data.SequenceEqual(r))
                    throw new Exception("Verify failed for " + what);
            }
        }

        public byte[] ReadFlash(UInt32 address, UInt32 length, string what = "NAND") => ReadFlash(fes, uboot, address, length, what);
        public byte[] ReadFlash(Stream fes, Stream uboot, UInt32 address, UInt32 length, bool closeStreams = false, string what = "NAND")
        {
            var result = ReadFlash(StreamHelpers.ReadToEnd(fes), StreamHelpers.ReadToEnd(uboot), address, length, what);
            if (closeStreams)
            {
                fes.Close();
                uboot.Close();
            }
            return result;
        }
        public  byte[] ReadFlash(byte[] fes, byte[] uboot, UInt32 address, UInt32 length, string what = "NAND")
        {
            var fel = new Fel();
            fel.WriteLine += WriteLine;
            fel.Fes1Bin = fes;
            fel.UBootBin = uboot;
            if (!fel.Open())
            {
                throw new Exception("USB Device Not Found");
            }

            return ReadFlash(fel, fes, uboot, address, length, what);
        }

        public byte[] ReadFlash(Fel fel, byte[] fes, byte[] uboot, UInt32 address, UInt32 length, string what = "NAND")
        {
            var size = length;
            size = (size + Fel.sector_size - 1) / Fel.sector_size;
            size = size * Fel.sector_size;

            long maxProgress = (size / 65536), progress = 0;

            var r = fel.ReadFlash(address, length,
                delegate (Fel.CurrentAction action, string command)
                {
                    switch (action)
                    {
                        case Fel.CurrentAction.ReadingMemory:
                            SetStatus("Reading " + what);
                            break;
                    }
                    progress++;
                    SetProgress?.Invoke(Math.Min(progress, maxProgress), maxProgress);
                }
            );

            return r;
        }

        public void RunCommand(string command, bool noreturn = false) => RunCommand(fes, uboot, command, noreturn);
        public void RunCommand(Stream fes, Stream uboot, string command, bool noreturn = false, bool closeStreams = false)
        {
            RunCommand(StreamHelpers.ReadToEnd(fes), StreamHelpers.ReadToEnd(uboot), command, noreturn);
            if (closeStreams)
            {
                fes.Close();
                uboot.Close();
            }
        }
        public void RunCommand(byte[] fes, byte[] uboot, string command, bool noreturn = false)
        {
            var fel = new Fel();
            fel.WriteLine += WriteLine;
            fel.Fes1Bin = fes;
            fel.UBootBin = uboot;
            if (!fel.Open())
            {
                throw new Exception("USB Device Not Found");
            }

            RunCommand(fel, command, noreturn);
        }
        public void RunCommand(Fel fel, string command, bool noreturn = false)
        {
            fel.RunUbootCmd(command, noreturn);
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
