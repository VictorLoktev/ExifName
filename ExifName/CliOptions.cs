using CommandLine.Text;
using CommandLine;

namespace ExifName
{
    [Verb("information", aliases: new string[] { "i", "info" }, HelpText = "Показать информацию о файле.")]
    public class CliOptionInformation
    {
        [Value(0, MetaName = "FileOrDirPath", Required = false, HelpText = "Путь к директории или файлу.")]
        public string? Path { get; set; }
    }


    [Verb("rename", aliases: new string[] { "r", "ren" }, HelpText = "Переименовать файлы в директории.")]
    public class CliOptionRename
    {
        [Value(0, MetaName = "FileOrDirPath", HelpText = "Путь к директории или файлу.")]
        public string? Path { get; set; }

        [Option("min", Default = "2004-03-01", Required = false, HelpText = "Минимальная допустимая дата в формате ГГГГ-ММ-ДД.")]
        public string? MinDateText
        {
            get => MinDate?.ToString("yyyy-MM-dd");
            set
            {
                if (DateTime.TryParseExact(value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime d))
                    MinDate = d;
                else
                    throw new Exception("Параметр min задается в формате ГГГГ-ММ-ДД.");
            }
        }
        public DateTime? MinDate;


        [Option("max", Default = "tomorrow", Required = false, HelpText = "Максимальная допустимая дата в формате ГГГГ-ММ-ДД.")]
        public string? MaxDateText
        {
            get => MaxDate?.ToString("yyyy-MM-dd");
            set
            {
                if (value != null && value.Equals("tomorrow", StringComparison.OrdinalIgnoreCase))
                    MaxDate = DateTime.Today.AddDays(1);
                else
                if (DateTime.TryParseExact(value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime d))
                    MaxDate = d;
                else
                    throw new Exception("Параметр max задается в формате ГГГГ-ММ-ДД.");
            }
        }
        public DateTime? MaxDate;


        [Option("priority", Default = 0, Required = false, HelpText = "Приоритет полей даты и времени (см. ниже).")]
        public int Priority { get; set; }

        [Option("template", Default = "yyyy'-'MM'-'dd'-'HHmmssf", Required = false, HelpText = "Шаблон названия файла в части даты и времени.")]
        public string? Template { get; set; }
    }

    public class CliOptions
    {
        [Usage(ApplicationAlias = "ExifName")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Информация о видео или фотографии", new CliOptionInformation { Path = "C:\\MyPhoto.jpg" });
                yield return new Example("Переименование фотографий в указанной папке", new CliOptionRename { Path = "C:\\Photo-Folder" });
                yield return new Example("Переименование фотографий в текущей папке", new CliOptionRename { Path = "" });
            }
        }
    }
}
