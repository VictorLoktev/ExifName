using System.Reflection;
using CommandLine;
using CommandLine.Text;

/*
 * Используется
 * MetadataExtractor: https://github.com/drewnoakes/metadata-extractor-dotnet
 * CommandLineParser: https://github.com/commandlineparser/
 */

namespace ExifName
{
    public class Program
    {
        static void Main(string[] args)
        {
            //string? version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

            //Console.WriteLine($"\r\nExifname v{version}\r\n");

            // Необходимо для загрузки зависимых сборок из ресурсов
            //AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            //            Processor processor = new Processor();
            //            processor.Run( args );

            var helpWriter = new StringWriter();
            var parser = new CommandLine.Parser(with => with.HelpWriter = helpWriter);
            _ = parser.ParseArguments<CliOptions, CliOptionInformation, CliOptionRename>(args)
                .WithParsed<CliOptionInformation>(opts => Processor.DisplayInformation(opts))
                .WithParsed<CliOptionRename>(opts => RenameDirectory(opts))
                .WithNotParsed(errs => DisplayHelp(errs, helpWriter));

            //Console.ReadKey();
        }

        public static void RenameDirectory(CliOptionRename options)
        {
            Console.WriteLine(HeadingInfo.Default);
            int exitCode = ProcessAndReturnCode(options);
            Environment.Exit(exitCode);
        }

        public static int ProcessAndReturnCode(CliOptionRename options)
        {
            if (string.IsNullOrWhiteSpace(options.Path))
            {
                Console.WriteLine(
                    "В параметрах не указан путь.\r\n" +
                    "Выполняется переименование файлов в текущей директории.");
                return Processor.ProcessDirectory(options);
            }

            if (System.IO.Directory.Exists(options.Path))
            {
                Console.WriteLine($"Выполняется переименование файлов в директории\r\n'{options.Path}'");
                return Processor.ProcessDirectory(options);
            }

            if (File.Exists(options.Path))
            {
                Console.WriteLine(
                    "В параметре указан путь к файлу вместо директории.\r\n" +
                    "Используйте \r\nexifname.exe --help\r\nдля помощи."
                    );
            }

            return 1;
        }

        public static int DisplayHelp(IEnumerable<Error> errs, TextWriter helpWriter)
        {
            if (errs.IsVersion())
            {
                Console.WriteLine(helpWriter.ToString());
            }
            else
            if (errs.IsHelp() || errs.Any())
            {
                Console.Write(helpWriter.ToString());
                Console.WriteLine(
                    "Минимальная и максимальная даты устанавливаются для защиты\r\n" +
                    "на случай, если у фотоаппарата сбита дата и в Exif записана ерунда.\r\n" +
                    "Для более точного управления переименованием файлов используйте\r\n" +
                    $"файл конфигурации - поместите пустой файл {Config.ConfigFileName}\r\n" +
                    $"в обрабатываемую директорию и запустите программу.\r\n" +
                    $"Программа заполнит пустой файл инструкцией по настройке\r\n" +
                    $"конфигурационного файла.\r\n" +
                    $"Следуйте инструкции и заполняйте конфигурационный файл.\r\n" +
                    $"Приоритет полей, из которых берется дата и время:\r\n" +
                    $"0 - (по умолчанию) самая ранняя дата-время из полей:\r\n" +
                    $"    'Date/Time', 'Date/Time Original', 'Date/Time Digitized'\r\n" +
                    $"1 - самая поздняя дата-время из полей:\r\n" +
                    $"    'Date/Time', 'Date/Time Original', 'Date/Time Digitized'\r\n" +
                    $"2 - 'Date/Time' > 'Date/Time Original' > 'Date/Time Digitized'\r\n" +
                    $"3 - 'Date/Time Digitized' > 'Date/Time Original' > 'Date/Time'\r\n" +
                    $"4 - 'Date/Time Original' > 'Date/Time' > 'Date/Time Digitized'\r\n" +
                    $"5 - 'Date/Time Original' > 'Date/Time Digitized' > 'Date/Time'\r\n" +
                    "     наибольший приоритет > наименьший приоритет"
                    );
            }
            else
            {
                Console.Error.WriteLine(helpWriter.ToString());
            }

            return -1;
        }

        // Source: https://stackoverflow.com/questions/10137937/merge-dll-into-exe
        private static Assembly? CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) => AssemblyResolve(args.Name);

        private static Assembly? AssemblyResolve(string name)
        {
            // Source: https://stackoverflow.com/questions/10137937/merge-dll-into-exe

            Assembly thisAssembly = Assembly.GetExecutingAssembly();

            //Get the Name of the AssemblyFile
            if (!name.EndsWith(".dll"))
            {
                int index = name.IndexOf(',');
                if (index > 0)
                    name = name.Substring(0, index);
                name += ".dll";
            }

            //Load form Embedded Resources - This Function is not called if the Assembly is in the Application Folder
            var resources = thisAssembly.GetManifestResourceNames().Where(s => s.EndsWith(name));
            if (resources.Any())
            {
                string resourceName = resources.First();
                using (Stream? stream = thisAssembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                        return null;
                    byte[] block = new byte[ stream.Length ];
                    _ = stream.Read(block, 0, block.Length);
                    Assembly asm = Assembly.Load(block);
                    AssemblyName[] asmNames = asm.GetReferencedAssemblies();
                    return asm;
                }
            }
            return null;
        }
    }
}
