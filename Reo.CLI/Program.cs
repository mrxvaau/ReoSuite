
using System;
using System.IO;
using System.Linq;
using System.Text;
using Reo.Core;

namespace Reo.CLI
{
    class Program
    {
        static void Usage()
        {
            Console.WriteLine("Reo CLI");
            Console.WriteLine("Usage: reo build <source.reo> [-o output.exe] [--emit-cs out.cs]");
        }

        static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
            {
                string srcPath = args[1];
                if (!File.Exists(srcPath)) { Console.Error.WriteLine("Source not found: " + srcPath); return 2; }
                string outExe = args.Contains("-o") ? args[Array.IndexOf(args, "-o") + 1]
                                                    : Path.ChangeExtension(srcPath, ".exe");
                string? outCs = args.Contains("--emit-cs") ? args[Array.IndexOf(args, "--emit-cs") + 1] : null;

                string reoSource = File.ReadAllText(srcPath);
                var (ok, cs, diags) = ReoCompiler.CompileToExe(reoSource, outExe);
                if (!string.IsNullOrEmpty(outCs)) File.WriteAllText(outCs, cs, Encoding.UTF8);

                if (ok)
                {
                    Console.WriteLine("Build OK -> " + outExe);
                    return 0;
                }
                else
                {
                    Console.WriteLine("Build FAILED");
                    Console.WriteLine(diags);
                    return 3;
                }
            }
            Usage();
            return 1;
        }
    }
}
