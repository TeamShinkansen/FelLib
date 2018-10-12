using CommandLine;
using System;
using System.Diagnostics;
using System.IO;

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
    
    public class Program
    {
        public static string ExeLocation
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().Location;
            }
        }

        static int Main(string[] args)
        {
            Debug.Listeners.Add(new TextWriterTraceListener(System.Console.Out));

            var result = CommandLine.Parser.Default.ParseArguments<MembootOptions, FlashBootOptions, FlashUbootOptions>(args).MapResult(
                (MembootOptions opts) =>
                {
                    if (opts.Fes.Exists && opts.Uboot.Exists && opts.BootImage.Exists)
                    {
                        FelHelpers.Helpers.Memboot(opts.Fes.OpenRead(), opts.Uboot.OpenRead(), opts.BootImage.OpenRead());
                        return 0;
                    }
                    return 1;
                },
                (FlashBootOptions opts) =>
                {
                    if (opts.Fes.Exists && opts.Uboot.Exists && opts.BootImage.Exists)
                    {
                        FelHelpers.Helpers.FlashBoot(opts.Fes.OpenRead(), opts.Uboot.OpenRead(), opts.BootImage.OpenRead());
                        return 0;
                    }
                    return 1;
                },
                (FlashUbootOptions opts) =>
                {
                    if (opts.Fes.Exists && opts.Uboot.Exists)
                    {
                        FelHelpers.Helpers.FlashUboot(opts.Fes.OpenRead(), opts.Uboot.OpenRead(), opts.FlashAll);
                    }
                    return 1;
                },
                errs => 1
            );
            Console.ReadLine();
            return result;
        }
    }
}
