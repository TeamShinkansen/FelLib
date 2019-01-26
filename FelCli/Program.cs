using CommandLine;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FelCli
{
    class FesUbootOptions {
        [Option('f', "fes", Required = true, HelpText = "The fes1 image to use")]
        public FileInfo Fes { get; set; }

        [Option('u', "uboot", Required = true, HelpText = "The uboot image to use")]
        public FileInfo Uboot { get; set; }
    }

    [Verb("memboot", HelpText = "Boot an android boot image from RAM.")]
    class MembootOptions : FesUbootOptions {
        [Option('b', "boot-image", Required = true, HelpText = "The android boot image to use")]
        public FileInfo BootImage { get; set; }
    }

    [Verb("flash-boot", HelpText = "Flash the android boot image")]
    class FlashBootOptions : FesUbootOptions {
        [Option('b', "boot-image", Required = true, HelpText = "The android boot image to use")]
        public FileInfo BootImage { get; set; }
    }

    [Verb("flash-uboot", HelpText = "Flash the uboot.")]
    class FlashUbootOptions : FesUbootOptions
    {
        [Option('a', "all", Default = false, HelpText = "Flash all copies of the uboot (dangerous!)")]
        public bool FlashAll { get; set; }
    }

    [Verb("flash-nand", HelpText = "Flash the NAND.")]
    class FlashNandOptions : FesUbootOptions
    {
        [Option('a', "address", Required = true, HelpText = "The nand address to flash")]
        public UInt32 Address { get; set; }

        [Option('i', "input", Required = true, HelpText = "The file to flash")]
        public FileInfo Input { get; set; }

        [Option('v', "verify", Default = false, HelpText = "Verify the written data")]
        public bool Verify { get; set; }
    }

    [Verb("read-nand", HelpText = "Read the NAND.")]
    class ReadNandOptions : FesUbootOptions
    {
        [Option('a', "address", Required = true, HelpText = "The nand address to read")]
        public UInt32 Address { get; set; }

        [Option('l', "length", Required = true, HelpText = "The length of the data to read")]
        public UInt32 Length { get; set; }

        [Option('o', "out", Required = true, HelpText = "The file to write")]
        public FileInfo Output { get; set; }
    }

    [Verb("run-command", HelpText = "Run a uboot command.")]
    class RunUbootCommandOptions : FesUbootOptions
    {
        [Option('c', "command", Required = true, HelpText = "The command to run")]
        public string Command { get; set; }

        [Option('n', "noreturn", Default = false, HelpText = "")]
        public bool noreturn { get; set; }
    }

    public class Program
    {
        public static string ExeLocation
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().Location;
            }
        }

        static async Task FelDevice()
        {
            if (!FelLib.Fel.DeviceExists())
            {
                Console.Write("Waiting for FEL device");
                while (!FelLib.Fel.DeviceExists())
                {
                    Console.Write(".");
                    await Task.Delay(1000);
                }
                Console.Write("\r\n");
            }
        }

        static int Main(string[] args)
        {
            try
            {


                var result = CommandLine.Parser.Default.ParseArguments<MembootOptions, FlashBootOptions, FlashUbootOptions, ReadNandOptions, FlashNandOptions, RunUbootCommandOptions>(args).MapResult(
                    (MembootOptions opts) =>
                    {
                        if (opts.Fes.Exists && opts.Uboot.Exists && opts.BootImage.Exists)
                        {
                            FelDevice().Wait();
                            var helpers = new FelHelpers.Helpers();
                            helpers.SetStatus += Console.WriteLine;
                            helpers.WriteLine += Console.WriteLine;
                            helpers.Memboot(opts.Fes.OpenRead(), opts.Uboot.OpenRead(), opts.BootImage.OpenRead());
                            return 0;
                        }
                        return 1;
                    },
                    (FlashBootOptions opts) =>
                    {
                        if (opts.Fes.Exists && opts.Uboot.Exists && opts.BootImage.Exists)
                        {
                            FelDevice().Wait();
                            var helpers = new FelHelpers.Helpers();
                            helpers.SetStatus += Console.WriteLine;
                            helpers.WriteLine += Console.WriteLine;
                            helpers.FlashBoot(opts.Fes.OpenRead(), opts.Uboot.OpenRead(), opts.BootImage.OpenRead());
                            return 0;
                        }
                        return 1;
                    },
                    (FlashUbootOptions opts) =>
                    {
                        if (opts.Fes.Exists && opts.Uboot.Exists)
                        {
                            FelDevice().Wait();
                            var helpers = new FelHelpers.Helpers();
                            helpers.SetStatus += Console.WriteLine;
                            helpers.WriteLine += Console.WriteLine;
                            helpers.FlashUboot(opts.Fes.OpenRead(), opts.Uboot.OpenRead(), opts.FlashAll);
                            return 0;
                        }
                        return 1;
                    },
                    (ReadNandOptions opts) =>
                    {
                        if (opts.Fes.Exists && opts.Uboot.Exists)
                        {
                            FelDevice().Wait();
                            var helpers = new FelHelpers.Helpers();
                            helpers.SetStatus += Console.WriteLine;
                            helpers.WriteLine += Console.WriteLine;
                            var data = helpers.ReadFlash(opts.Fes.OpenRead(), opts.Uboot.OpenRead(), opts.Address, opts.Length, true);
                            using (var file = opts.Output.Open(FileMode.Create))
                            {
                                file.Write(data, 0, data.Length);
                                file.Close();
                            }
                            return 0;
                        }
                        return 1;
                    },
                    (FlashNandOptions opts) =>
                    {
                        if (opts.Fes.Exists && opts.Uboot.Exists)
                        {
                            FelDevice().Wait();
                            var helpers = new FelHelpers.Helpers();
                            helpers.SetStatus += Console.WriteLine;
                            helpers.WriteLine += Console.WriteLine;
                            helpers.WriteFlash(opts.Fes.OpenRead(), opts.Uboot.OpenRead(), opts.Address, opts.Input.OpenRead(), true, opts.Verify);
                            return 0;
                        }
                        return 1;
                    },
                    (RunUbootCommandOptions opts) =>
                    {
                        if (opts.Fes.Exists && opts.Uboot.Exists)
                        {
                            FelDevice().Wait();
                            var helpers = new FelHelpers.Helpers();
                            helpers.SetStatus += Console.WriteLine;
                            helpers.WriteLine += Console.WriteLine;
                            helpers.RunCommand(opts.Fes.OpenRead(), opts.Uboot.OpenRead(), opts.Command, opts.noreturn, true);
                            return 0;
                        }
                        return 1;
                    },
                    errs => 1
                );

                Console.ReadLine();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.InnerException);
                Console.ReadLine();
                return 1;
            }
        }
    }
}
