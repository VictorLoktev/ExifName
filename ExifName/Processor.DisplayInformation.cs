using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace ExifName
{
    public partial class Processor
    {
        #region ProcessFile - информация о файле

        public static int DisplayInformation(CliOptionInformation options)
        {
            string[] files;

            if (string.IsNullOrWhiteSpace(options.Path))
            {
                files = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "*", SearchOption.TopDirectoryOnly);
            }
            else
            if (System.IO.Directory.Exists(options.Path))
            {
                files = System.IO.Directory.GetFiles(options.Path, "*", SearchOption.TopDirectoryOnly);
            }
            else
            if (File.Exists(options.Path))
            {
                files = new string[] { options.Path };
            }
            else
            {
                Console.WriteLine(
                    "В параметре указан путь к несуществующему файлу или директории.\r\n" +
                    "Используйте \r\nexifname.exe --help\r\nдля помощи."
                    );
                return 1;
            }

            foreach (string file in files)
            {

                IReadOnlyList<MetadataExtractor.Directory> meta;
                try
                {
                    meta = ImageMetadataReader.ReadMetadata(file);
                }
                catch (System.IO.IOException)
                {
                    // Файл не доступен, вероятно пытаемся открыть файл, в который перенаправлен вывод консоли
                    continue;
                }
                catch (ImageProcessingException)
                {
                    // Это не фото или видео с EXIF, пропускаем
                    continue;
                }

                Console.WriteLine($"\r\n--------  {System.IO.Path.GetFileName(file)}  --------");

                foreach (MetadataExtractor.Directory directory in meta)
                {
                    Console.WriteLine(directory.Name);

                    foreach (Tag tag in directory.Tags)
                    {
                        if (directory.GetType() == typeof(ExifIfd0Directory))
                        {
                            switch (tag.Type)
                            {
                            case ExifIfd0Directory.TagModel:
                            case ExifIfd0Directory.TagMake:
                            case ExifIfd0Directory.TagBodySerialNumber:
                                Console.WriteLine($"{tag}     => CAMERA in config");
                                break;
                            case ExifIfd0Directory.TagCameraOwnerName:
                                Console.WriteLine($"{tag}     => OWNER in config");
                                break;
                            case ExifIfd0Directory.TagDateTime:
                                Console.WriteLine($"{tag}     => DATE/TIME");
                                break;
                            default:
                                Console.WriteLine(tag);
                                break;
                            }
                        }
                        else
                        if (directory.GetType() == typeof(ExifSubIfdDirectory))
                        {
                            switch (tag.Type)
                            {
                            case ExifSubIfdDirectory.TagDateTimeOriginal:
                                Console.WriteLine($"{tag}     => DATE/TIME Original");
                                break;
                            case ExifSubIfdDirectory.TagDateTimeDigitized:
                                Console.WriteLine($"{tag}     => DATE/TIME Digitized");
                                break;
                            default:
                                Console.WriteLine(tag);
                                break;
                            }
                        }
                        else
                        if (directory.GetType() == typeof(QuickTimeMovieHeaderDirectory))
                        {
                            switch (tag.Type)
                            {
                            case QuickTimeMovieHeaderDirectory.TagCreated:
                                Console.WriteLine($"{tag}     => DATE/TIME");
                                break;
                            default:
                                Console.WriteLine(tag);
                                break;
                            }
                        }
                        else
                        {
                            Console.WriteLine(tag);
                        }
                    }
                }
            }

            return 0;
        }

        #endregion
        #region Вспомогательные функции

        private static DateTime MergeParts(DateTime baseDateTime, int? subSecondPart)
        {
            if (subSecondPart == null)
                return baseDateTime;

            /*
             * Как написано здесь
             * https://en.wikipedia.org/wiki/Exif
             * в теге с долями секунды могут быть десятые, сотые, тысячные и десятитысячные доли секунды.
             * Телефоны Samsung сюда пишут числа из 4-х цифр (например, 0123),
             * при этом подразумевая, что это 123 миллисекунды.
             * На случай, если все же будет число равное или превышающее 1000,
             * делается обработка этого случая с микросекундами, хотя это и не требуется.
             * Нам дробная часть секунд нужна, по большому счету, только при сортировке фотографий,
             * сделанных быстро-серийной съемкой.
             * [Exif SubIFD] Sub-Sec Time Original - 0854
             */

            TimeSpan span =
                subSecondPart.Value >= 1000
                ? new TimeSpan(0, 0, 0, 0, subSecondPart.Value / 10, subSecondPart.Value % 10)
                : new TimeSpan(0, 0, 0, 0, subSecondPart.Value);

            return baseDateTime.Add(span);
        }

        private static TimeSpan? GetTimeZone(string? zone)
        {
            if (string.IsNullOrWhiteSpace(zone))
                return null;

            // [Exif SubIFD] Time Zone Original - +08:00
            string[] parts = zone.Split(new char[] { '+', '-', ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return null;

            int h = int.Parse(parts[ 0 ]);
            int m = int.Parse(parts[ 1 ]);
            if (zone.Contains('-'))
                h = -h;
            return new TimeSpan(h, m, 0);
        }

        private static ConfigInfo? FindCameraConfig(string fileExtension, string camera)
        {
            foreach (ConfigInfo info in Config.Items)
            {
                if (!info.FileExtension.Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(info.Camera) ||
                    (camera ?? "").Trim().ContainsIgnoreCase(info.Camera))
                {
                    return info;
                }
            }

            return null;
        }

        #endregion
    }
}
