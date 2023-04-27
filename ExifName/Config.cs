using System.Text.RegularExpressions;

namespace ExifName
{
    #region Типы данных

    public struct ConfigInfo
    {
        public TimeSpan TimeShift;
        public string Camera;
        public string FileExtension;

        public ConfigInfo(string ext, TimeSpan timeShift, string camera)
        {
            FileExtension = ext;
            TimeShift = timeShift;
            Camera = camera;
        }
    }

    #endregion

    public static class Config
    {
        /// <summary>
        /// <para>Шаблон заполнения строк конфигурационного файла.</para>
        /// </summary>
        public const string ConfigRegexString =
            @"^\s*(?<ext>\..+)\s+(?<sign>[+-])(?<time>\d\d?:\d\d?(:\d\d?(\.\d+)?)?)\s*(?<camera>.*)\s*$";
        /// <summary>
        /// <para>Regex чтения строк конфигурационного файла.</para>
        /// </summary>
        public static readonly Regex ConfigRegex =
            new Regex(ConfigRegexString, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// <para>Название файла с конфигурацией для программы.</para>
        /// <para>Файл располагается непосредственно в папке с фото и видео.</para>
        /// </summary>
        public const string ConfigFileName = "exifname.config";

        /// <summary>
        /// <para>Список строк с настройками обработки файлов.</para>
        /// </summary>
        public static List<ConfigInfo> Items { get; private set; } = new List<ConfigInfo>();


        /// <summary>
        /// <para>Загрузка конфигурации предстоящей обработки файлов в выбранной директории.</para>
        /// </summary>
        /// <param name="srcDir">Путь к обрабатываемой директории.</param>
        public static void Load(string srcDir)
        {
            // Очистка конфигурации, на случай, если обрабатывается несколько директорий и конфигурация загружается в цикле
            Items.Clear();

            string configFilePath = Path.Combine(srcDir, ConfigFileName);
            if (!File.Exists(configFilePath))
            {
                Console.WriteLine($"'{ConfigFileName}' не обнаружен, настройки по умолчанию.");
                return;
            }

            Console.WriteLine($"Конфигурационный файл: '{configFilePath}'");

            string[] configLines = File.ReadAllLines(configFilePath);

            bool emptyConfig = true;

            for (int line = 0; line < configLines.Length; line++)
            {
                string configLine = configLines[ line ].Trim();
                if (string.IsNullOrWhiteSpace(configLine))
                    continue;

                emptyConfig = false;

                if (configLine.StartsWith("//") ||
                    configLine.StartsWith("#") ||
                    configLine.StartsWith("--") ||
                    configLine.StartsWith("REM"))
                {
                    // Вся строка - это комментарий
                    continue;
                }

                Match m = ConfigRegex.Match(configLine);
                if (!m.Success ||
                    !m.Groups[ "ext" ].Success ||
                    !m.Groups[ "sign" ].Success ||
                    !m.Groups[ "time" ].Success ||
                    !m.Groups[ "camera" ].Success ||
                    !TimeSpan.TryParse(m.Groups[ "time" ].Value, out TimeSpan span))
                {
                    Console.WriteLine(
                        $"Нераспознанный синтаксис строки {line + 1} конфигурационного файла папки.\r\n" +
                        "#    Полный формат строки конфигурации:\r\n" +
                        "#    .EXT <знак>HH:MM[:SS[.ttt]][<подстрока CAMERA>]\r\n" +
                        "#    .EXT - расширение файла, точка обязательная, регистр не важен, пример: .jpg или .mp4\r\n" +
                        "#    <знак> - знак плюс или минус, обязательный, пример: + или -\r\n" +
                        "#    Пример: .jpg +03:00: samsung\r\n"
                        );
                    return;
                }

                string camera = m.Groups[ "camera" ].Value?.Trim() ?? "";
                string ext = m.Groups[ "ext" ].Value?.Trim() ?? "";

                Items.Add(new ConfigInfo(ext, span, camera));
            }
            if (emptyConfig)
            {
                /*
                 * Если config-файл задан, но в нем ничего нет,
                 * пишем в него инструкции как пользоваться config-ом.
                 */

                File.WriteAllText(configFilePath,
                    "# Конфигурационный файл программы ExifName.\r\n" +
                    "# Действие конфигурационного файла распространяется на все файлы данной папки.\r\n" +
                    "# Формат файла:\r\n" +
                    "# 1) Символы #, -- или // в начале строки указывают на комментарий.\r\n" +
                    "# 2) В строке задается временная зона (смещение) для конкретного фотоаппарата,\r\n" +
                    "#    фотоаппарат задается подстрокой параметра CAMERA\r\n" +
                    "#    (см. exif-информацию, выдаваемую программой по файлу вызовом ExifName <filename>).\r\n" +
                    "#    Смещение временной зоны записывается в формате <знак>HH:MM[:SS[.ttt]],\r\n" +
                    "#    где <знак> один из символов: + или -.\r\n" +
                    "#    Если знак не задан, будет ошибка.\r\n" +
                    "#    Полный формат строки конфигурации:\r\n" +
                    "#    .ext <знак>HH:MM[:SS[.ttt]][<подстрока CAMERA>]\r\n" +
                    "#\r\n" +
                    "#    Видео файлы от телефонов не содержат информации о телефоне чтобы разделить файлы по аппаратам!\r\n" +
                    "#    Для видео-файлов следует указывать владельца \"VIDEDO\".\r\n" +
                    "#    Допустимо указывать смещение для одной камеры, а затем общее смещение без указания названия камеры,\r\n" +
                    "#    Последнее будет использовано для видео файлов и всех, кто не подпадает под указанное название\r\n" +
                    "# \r\n" +
                    "# .jpg +03:00 SAMSUNG\r\n" +
                    "# .mov +03:00 OLIMPUS\r\n" +
                    "# .mp4 +03:00\r\n" +
                    "#\r\n" +
                    "#\r\n\r\n\r\n"
                    );
                Console.WriteLine($"Конфигурационный файл {ConfigFileName} в папке заполнен комментарием.");
            }
        }
    }
}
