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
            var helpWriter = new StringWriter();
            var parser = new CommandLine.Parser(with => with.HelpWriter = helpWriter);
            _ = parser.ParseArguments<CliOptions, CliOptionInformation, CliOptionRename>(args)
                .WithParsed<CliOptionInformation>(opts => Processor.DisplayInformation(opts))
                .WithParsed<CliOptionRename>(opts => RenameDirectory(opts))
                .WithNotParsed(errs => DisplayHelp(errs, helpWriter));
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
                    "на случай, если у фотоаппарата дата обнулена, затем попала в Exif.\r\n" +
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
    }
}
