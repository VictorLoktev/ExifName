using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace ExifName
{
    public partial class Processor
    {
        #region Константы и переменные

        //const string FileMaskToRenameRegex =
        //@"^(?:(DSC|IMG|PIC|P|PA|PB|PC|Photo|Video)[_-]?\d+|(?:[0-9]|(?:-|_)[0-9])+)(?'number_in_brackets'\(\d+\))?(?'name'\s*-\s*.*|\D\D.*)?$",
        public const string FileMaskToRenameRegex = @"^(?'name'(?:\p{L}|[()_]|\d| \d|-|(<!- ))+)(?'comment'.*)$";


        #endregion
        #region ProcessDirectory - переименование файлов в директории

        public static int ProcessDirectory(CliOptionRename options)
        {
            string workingDirectory = string.IsNullOrWhiteSpace(options.Path)
                ? Environment.CurrentDirectory
                : options.Path;

            Console.Out.WriteLine($"Обрабатывается директория:\r\n{workingDirectory}");

            #region Чтение файла конфигурации

            // Конфигурационный файл в обрабатываемой папке
            Config.Load(workingDirectory);

            #endregion

            try
            {
                // Переименование файлов в директории
                return RenameFiles(workingDirectory, options);
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine("Ошибка:\r\n\r\n{0}\r\n\r\n", ex.Message);
                return 1;
            }
        }

        public static int RenameFiles(string workingDirectory, CliOptionRename options)
        {
            /*
            Отлавливаем в названиях файлов два варианта конструкции:
            Первый - буквы и цифры до пробела, пробел здесь разделитель части нумерации файла и комментария в названии.
            Второй для файлов вида "Картинка 1.jpg".
            Поэтому к номерной части относим все, включая пробел, после которого цифра,
            а пробел, после которого нет цифры относим к комментарию.

            https://stackoverflow.com/questions/28156769/foreign-language-characters-in-regular-expression-in-c-sharp
            */

            Regex rx = new Regex(FileMaskToRenameRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            List<ExifFileInfo> FileList = new List<ExifFileInfo>();
            string[] files = System.IO.Directory.GetFiles(workingDirectory, "*", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                try
                {
                    IReadOnlyList<MetadataExtractor.Directory> directories;
                    try
                    {
                        directories = ImageMetadataReader.ReadMetadata(file);
                    }
                    catch
                    {
                        continue;
                    }

                    #region Чтение информации о файле

                    ExifFileInfo info = new ExifFileInfo();

                    // Название файла
                    info.OriginalName = file;
                    info.Extention = Path.GetExtension(info.OriginalName).ToLower();
                    info.NameWithoutExtention = Path.GetFileNameWithoutExtension(info.OriginalName);
                    info.NameDescription = "";

                    DateTime datetime;
                    int subSec;

                    var pic0Exif = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                    var picSubExif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                    var movExif = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();

                    #region Date/Time просто

                    DateTime? simpleDateTime = null;
                    TimeSpan? simpleTimeZone = null;

                    if (pic0Exif != null &&
                        pic0Exif.TryGetDateTime(ExifIfd0Directory.TagDateTime, out datetime))
                    {
                        if (picSubExif != null &&
                            picSubExif.TryGetInt32(ExifSubIfdDirectory.TagSubsecondTime, out subSec))
                        {
                            simpleDateTime = MergeParts(datetime, subSec);
                        }
                        else
                        {
                            simpleDateTime = MergeParts(datetime, null);
                        }

                        simpleTimeZone = picSubExif != null
                            ? GetTimeZone(picSubExif.GetDescription(ExifSubIfdDirectory.TagTimeZone))
                            : null;
                    }

                    #endregion
                    #region Date/Time Original

                    DateTime? originalDateTime = null;
                    TimeSpan? originalTimeZone = null;

                    if (picSubExif != null &&
                        picSubExif.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out datetime))
                    {
                        if (picSubExif.TryGetInt32(ExifSubIfdDirectory.TagSubsecondTimeOriginal, out subSec))
                            originalDateTime = MergeParts(datetime, subSec);
                        else
                            originalDateTime = MergeParts(datetime, null);

                        originalTimeZone = GetTimeZone(picSubExif.GetDescription(ExifSubIfdDirectory.TagTimeZoneOriginal));
                    }

                    #endregion
                    #region Date/Time Digitized

                    DateTime? digitizedDateTime = null;
                    TimeSpan? digitizedTimeZone = null;

                    if (picSubExif != null &&
                        picSubExif.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeDigitized, out datetime))
                    {
                        if (picSubExif.TryGetInt32(ExifSubIfdDirectory.TagSubsecondTimeDigitized, out subSec))
                            digitizedDateTime = MergeParts(datetime, subSec);
                        else
                            digitizedDateTime = MergeParts(datetime, null);

                        digitizedTimeZone = GetTimeZone(picSubExif.GetDescription(ExifSubIfdDirectory.TagTimeZoneDigitized));
                    }

                    #endregion
                    #region Камера, модель, производитель и другое

                    if (pic0Exif != null)
                    {
                        info.CameraModel = pic0Exif.GetString(ExifDirectoryBase.TagModel);
                        info.CameraMaker = pic0Exif.GetString(ExifDirectoryBase.TagMake);
                        info.CameraInternalSerialNumber = pic0Exif.GetString(ExifDirectoryBase.TagBodySerialNumber);
                        info.CameraOwner = pic0Exif.GetString(ExifDirectoryBase.TagCameraOwnerName);
                        info.IsVideo = false;
                    }

                    #endregion

                    if (movExif != null)
                    {
                        info.IsVideo = true;
                        foreach (int tag in new int[] { QuickTimeMovieHeaderDirectory.TagCreated })
                        {
                            if (movExif.TryGetDateTime(tag, out datetime))
                            {
                                var major = directories.OfType<QuickTimeFileTypeDirectory>().FirstOrDefault();
                                if (major != null)
                                {
                                    switch ((major.GetString(QuickTimeFileTypeDirectory.TagMajorBrand) ?? "").ToLower().Trim())
                                    {
                                    // смещение не нужно
                                    case "3gp5":// Телефон типа HTC Hero
                                    case "qt":  // QuickTime
                                        simpleTimeZone = TimeSpan.Zero;
                                        break;

                                    // Смещение нужно
                                    // OLYMPUS E-P5
                                    // Samsung Galaxy 
                                    case "isom":
                                    // Samsung Galaxy 
                                    // Samsung S20+
                                    case "mp42":
                                    // Телефон
                                    // Телефон типа HTC Hero
                                    case "3gp":
                                    // Телефон
                                    case "3gp2":
                                    // Телефон
                                    case "3gp3":
                                    case "3gp4":
                                    // Видео с фотоаппарата
                                    case "m4v":
                                        //currentOffset = TimeZone.CurrentTimeZone.GetUtcOffset( DateTime.Now );
                                        /*
                                         * Для данного типа видео в файл время съемки пишется в часовом поясе UTC.
                                         * Либо должен быть конфигурационный файл с установленным часовым поясом,
                                         * либо должны быть фотографии в папке, тогда по часовому поясу будет делаться смещение для видео.
                                         * Если нет ни того, ни другого, часовой пояс задается по дате из файла, когда файл
                                         * переписывается с камеры/телефона на диск ПК, обычно это тот же часовой пояс,
                                         * что установлен на ПК в момент копирования файла.
                                         */
                                        FileInfo fInfo = new FileInfo(file);
                                        DateTime d1 = fInfo.LastWriteTimeUtc;
                                        DateTime d2 = fInfo.LastWriteTime;
                                        simpleTimeZone = d2.Subtract(d1);
                                        break;
                                    default:
                                        Console.Error.WriteLine($"Файл '{info.NameWithoutExtention}{info.Extention}' " +
                                            $"содержит неизвестный Major Brand '{(major.GetString(QuickTimeFileTypeDirectory.TagMajorBrand) ?? "")}'");
                                        break;
                                    }
                                }

                                ConfigInfo? videoInf = FindCameraConfig(info.Extention, "VIDEO") ?? FindCameraConfig(info.Extention, "");

                                if (Config.Items.Count > 0 && !videoInf.HasValue)
                                {
                                    Console.WriteLine("В конфигурационном файле не задана настройка для \"VIDEO\"");
                                    return 1;
                                }

                                simpleDateTime = datetime + (videoInf?.TimeShift ?? simpleTimeZone);
                                break;
                            }
                        }
                    }

                    #region Восстановление отсутствующих временных зон

                    /*
                     * Временные зоны (time zone) есть не у всех полей в EXIF,
                     * поэтому в случае, когда время и дата в полях совпадают,
                     * отсутствующие временные зоны полей полагаются равными тем,
                     * которые в EXIF заданы.
                     */
                    if (!simpleTimeZone.HasValue &&
                        originalTimeZone.HasValue &&
                        simpleDateTime.HasValue &&
                        originalDateTime.HasValue &&
                        simpleDateTime == originalDateTime)
                    {
                        simpleTimeZone = originalTimeZone;
                    }
                    if (!simpleTimeZone.HasValue &&
                        digitizedTimeZone.HasValue &&
                        simpleDateTime.HasValue &&
                        digitizedDateTime.HasValue &&
                        simpleDateTime == digitizedDateTime)
                    {
                        simpleTimeZone = digitizedTimeZone;
                    }

                    if (!originalTimeZone.HasValue &&
                        simpleTimeZone.HasValue &&
                        simpleDateTime.HasValue &&
                        originalDateTime.HasValue &&
                        originalDateTime == simpleDateTime)
                    {
                        originalTimeZone = simpleTimeZone;
                    }
                    if (!originalTimeZone.HasValue &&
                        digitizedTimeZone.HasValue &&
                        originalDateTime.HasValue &&
                        digitizedDateTime.HasValue &&
                        originalDateTime == digitizedDateTime)
                    {
                        originalTimeZone = digitizedTimeZone;
                    }

                    if (!digitizedTimeZone.HasValue &&
                        simpleTimeZone.HasValue &&
                        simpleDateTime.HasValue &&
                        digitizedDateTime.HasValue &&
                        digitizedDateTime == simpleDateTime)
                    {
                        digitizedTimeZone = simpleTimeZone;
                    }
                    if (!digitizedTimeZone.HasValue &&
                        originalTimeZone.HasValue &&
                        originalDateTime.HasValue &&
                        digitizedDateTime.HasValue &&
                        digitizedDateTime == originalDateTime)
                    {
                        digitizedTimeZone = originalTimeZone;
                    }

                    #region Восстановление отсутствующих часовых поясов в полях даты и времени съемки

                    /*
                     * Часовые пояса (time zone) есть не у всех полей в EXIF,
                     * поэтому в случае, когда время и дата в полях совпадают,
                     * отсутствующие часовые пояса полей полагаются равными тем,
                     * которые в EXIF заданы.
                     */
                    if (!simpleTimeZone.HasValue &&
                        originalTimeZone.HasValue &&
                        simpleDateTime.HasValue &&
                        originalDateTime.HasValue &&
                        simpleDateTime == originalDateTime)
                    {
                        simpleTimeZone = originalTimeZone;
                    }
                    if (!simpleTimeZone.HasValue &&
                        digitizedTimeZone.HasValue &&
                        simpleDateTime.HasValue &&
                        digitizedDateTime.HasValue &&
                        simpleDateTime == digitizedDateTime)
                    {
                        simpleTimeZone = digitizedTimeZone;
                    }

                    if (!originalTimeZone.HasValue &&
                        simpleTimeZone.HasValue &&
                        simpleDateTime.HasValue &&
                        originalDateTime.HasValue &&
                        originalDateTime == simpleDateTime)
                    {
                        originalTimeZone = simpleTimeZone;
                    }
                    if (!originalTimeZone.HasValue &&
                        digitizedTimeZone.HasValue &&
                        originalDateTime.HasValue &&
                        digitizedDateTime.HasValue &&
                        originalDateTime == digitizedDateTime)
                    {
                        originalTimeZone = digitizedTimeZone;
                    }

                    if (!digitizedTimeZone.HasValue &&
                        simpleTimeZone.HasValue &&
                        simpleDateTime.HasValue &&
                        digitizedDateTime.HasValue &&
                        digitizedDateTime == simpleDateTime)
                    {
                        digitizedTimeZone = simpleTimeZone;
                    }
                    if (!digitizedTimeZone.HasValue &&
                        originalTimeZone.HasValue &&
                        originalDateTime.HasValue &&
                        digitizedDateTime.HasValue &&
                        digitizedDateTime == originalDateTime)
                    {
                        digitizedTimeZone = originalTimeZone;
                    }

                    #endregion
                    #region Выбор поля с датой и временем в соответствии с приоритетом

                    info.TimeOffset = TimeSpan.Zero;

                    switch (options.Priority)
                    {
                    case 0:
                        // 0 - (по умолчанию) самая ранняя дата-время из полей
                        info.PhotoDateTime = DateTime.MaxValue;

                        if (simpleDateTime.HasValue &&
                            info.PhotoDateTime >= simpleDateTime.Value &&
                            simpleDateTime.Value >= options.MinDate &&
                            simpleDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = simpleDateTime;
                            info.TimeOffset = simpleTimeZone;
                        }

                        if (originalDateTime.HasValue &&
                            info.PhotoDateTime >= originalDateTime.Value &&
                            originalDateTime.Value >= options.MinDate &&
                            originalDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = originalDateTime;
                            info.TimeOffset = originalTimeZone;
                        }

                        if (digitizedDateTime.HasValue &&
                            info.PhotoDateTime >= digitizedDateTime.Value &&
                            digitizedDateTime.Value >= options.MinDate &&
                            digitizedDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = digitizedDateTime;
                            info.TimeOffset = digitizedTimeZone;
                        }

                        if (info.PhotoDateTime == DateTime.MaxValue)
                        {
                            info.PhotoDateTime = null;
                            info.TimeOffset = null;
                        }
                        else
                        {
                            info.TimeOffset ??= simpleTimeZone ?? originalTimeZone ?? digitizedTimeZone;
                        }
                        break;
                    case 1:
                        // 1 - самая поздняя дата-время из полей
                        info.PhotoDateTime = DateTime.MinValue;

                        if (simpleDateTime.HasValue &&
                            info.PhotoDateTime <= simpleDateTime.Value &&
                            simpleDateTime.Value >= options.MinDate &&
                            simpleDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = simpleDateTime;
                            info.TimeOffset = simpleTimeZone;
                        }

                        if (originalDateTime.HasValue &&
                            info.PhotoDateTime <= originalDateTime.Value &&
                            originalDateTime.Value >= options.MinDate &&
                            originalDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = originalDateTime;
                            info.TimeOffset = originalTimeZone;
                        }

                        if (digitizedDateTime.HasValue &&
                            info.PhotoDateTime <= digitizedDateTime.Value &&
                            digitizedDateTime.Value >= options.MinDate &&
                            digitizedDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = digitizedDateTime;
                            info.TimeOffset = digitizedTimeZone;
                        }

                        if (info.PhotoDateTime == DateTime.MinValue)
                        {
                            info.PhotoDateTime = null;
                            info.TimeOffset = null;
                        }
                        else
                        {
                            info.TimeOffset ??= simpleTimeZone ?? originalTimeZone ?? digitizedTimeZone;
                        }
                        break;
                    case 2:
                        // 2 - 'Date/Time' > 'Date/Time Original' > 'Date/Time Digitized'
                        info.PhotoDateTime = null;
                        if (simpleDateTime.HasValue &&
                            simpleDateTime.Value >= options.MinDate &&
                            simpleDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = simpleDateTime;
                            info.TimeOffset = simpleTimeZone;
                        }
                        else
                        if (originalDateTime.HasValue &&
                            originalDateTime.Value >= options.MinDate &&
                            originalDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = originalDateTime;
                            info.TimeOffset = originalTimeZone;
                        }
                        else
                        if (digitizedDateTime.HasValue &&
                            digitizedDateTime.Value >= options.MinDate &&
                            digitizedDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = digitizedDateTime;
                            info.TimeOffset = digitizedTimeZone;
                        }
                        if (info.PhotoDateTime.HasValue)
                            info.TimeOffset ??= simpleTimeZone ?? originalTimeZone ?? digitizedTimeZone;
                        break;
                    case 3:
                        // 3 - 'Date/Time Digitized' > 'Date/Time Original' > 'Date/Time'
                        info.PhotoDateTime = null;
                        if (digitizedDateTime.HasValue &&
                            digitizedDateTime.Value >= options.MinDate &&
                            digitizedDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = digitizedDateTime;
                            info.TimeOffset = digitizedTimeZone;
                        }
                        else
                        if (originalDateTime.HasValue &&
                            originalDateTime.Value >= options.MinDate &&
                            originalDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = originalDateTime;
                            info.TimeOffset = originalTimeZone;
                        }
                        else
                        if (simpleDateTime.HasValue &&
                            simpleDateTime.Value >= options.MinDate &&
                            simpleDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = simpleDateTime;
                            info.TimeOffset = simpleTimeZone;
                        }
                        if (info.PhotoDateTime.HasValue)
                            info.TimeOffset ??= simpleTimeZone ?? originalTimeZone ?? digitizedTimeZone;
                        break;
                    case 4:
                        // 4 - 'Date/Time Original' > 'Date/Time' > 'Date/Time Digitized'
                        info.PhotoDateTime = null;
                        if (originalDateTime.HasValue &&
                            originalDateTime.Value >= options.MinDate &&
                            originalDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = originalDateTime;
                            info.TimeOffset = originalTimeZone;
                        }
                        else
                        if (simpleDateTime.HasValue &&
                            simpleDateTime.Value >= options.MinDate &&
                            simpleDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = simpleDateTime;
                            info.TimeOffset = simpleTimeZone;
                        }
                        else
                        if (digitizedDateTime.HasValue &&
                            digitizedDateTime.Value >= options.MinDate &&
                            digitizedDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = digitizedDateTime;
                            info.TimeOffset = digitizedTimeZone;
                        }
                        if (info.PhotoDateTime.HasValue)
                            info.TimeOffset ??= simpleTimeZone ?? originalTimeZone ?? digitizedTimeZone;
                        break;
                    case 5:
                        // 5 - 'Date/Time Original' > 'Date/Time Digitized' > 'Date/Time'
                        info.PhotoDateTime = null;
                        if (originalDateTime.HasValue &&
                            originalDateTime.Value >= options.MinDate &&
                            originalDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = originalDateTime;
                            info.TimeOffset = originalTimeZone;
                        }
                        else
                        if (digitizedDateTime.HasValue &&
                            digitizedDateTime.Value >= options.MinDate &&
                            digitizedDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = digitizedDateTime;
                            info.TimeOffset = digitizedTimeZone;
                        }
                        else
                        if (simpleDateTime.HasValue &&
                            simpleDateTime.Value >= options.MinDate &&
                            simpleDateTime.Value <= options.MaxDate
                            )
                        {
                            info.PhotoDateTime = simpleDateTime;
                            info.TimeOffset = simpleTimeZone;
                        }
                        if (info.PhotoDateTime.HasValue)
                            info.TimeOffset ??= simpleTimeZone ?? originalTimeZone ?? digitizedTimeZone;
                        break;
                    default:
                        throw new Exception("Задан неизвестный приоритет полей для даты и времени");
                    }

                    #endregion

                    // Файл пропускается, если в нем нет даты в EXIF или дата не удовлетворяет заданному интервалу min-max
                    if (!info.PhotoDateTime.HasValue)
                    {
                        if (pic0Exif != null || picSubExif != null || movExif != null)
                        {
                            // Если EXIF в файле есть, то выдаем предупреждение
                            Console.Error.WriteLine(
                                $"EXIF в файле '{info.NameWithoutExtention}{info.Extention}'\r\n" +
                                $"содержит даты вне допустимого диапазона " +
                                $"{options.MinDate:dd.MM.yyyy} - {options.MaxDate:dd.MM.yyyy}:\r\n" +
                                $"   '{simpleDateTime:dd.MM.yyyy HH:mm:ss}' Date/Time\r\n" +
                                $"   '{originalDateTime:dd.MM.yyyy HH:mm:ss}' Date/Time Original\r\n" +
                                $"   '{digitizedDateTime:dd.MM.yyyy HH:mm:ss}' Date/Time Digitized\r\n" +
                                $"проверьте настройки фотоаппарата!");
                        }

                        continue;
                    }


                    ConfigInfo? inf = null;
                    if (!inf.HasValue && !string.IsNullOrEmpty(info.CameraInternalSerialNumber))
                        inf = FindCameraConfig(info.Extention, info.CameraInternalSerialNumber);

                    if (!inf.HasValue && !string.IsNullOrEmpty(info.CameraOwner))
                        inf = FindCameraConfig(info.Extention, info.CameraOwner);

                    if (!inf.HasValue && !string.IsNullOrEmpty(info.CameraModel))
                        inf = FindCameraConfig(info.Extention, info.CameraModel);

                    if (!inf.HasValue && !string.IsNullOrEmpty(info.CameraMaker))
                        inf = FindCameraConfig(info.Extention, info.CameraMaker);

                    if (!inf.HasValue && Config.Items.Count > 0)
                    {
                        Console.WriteLine(
                            "В конфигурационном файле отсутствует настройка для обработки:\r\n" +
                            $"для файла: {info.OriginalName}\r\n" +
                            $"Серийный номер: {info.CameraInternalSerialNumber}\r\n" +
                            $"Владелец: {info.CameraOwner}\r\n" +
                            $"Модель: {info.CameraModel}\r\n" +
                            $"Производитель: {info.CameraMaker}\r\n"
                            );
                        return 1;
                    }

                    #endregion

                    #region Если названию файла уже добавлено текстовое название

                    if (info != null)
                    {
                        // Попытка вычленить комментарий к фотографии в названии файла.
                        Match m = rx.Match(info.NameWithoutExtention);
                        if (m.Success)
                        {
                            info.NameDescription = m.Groups[ "comment" ]?.Value;
                        }
                    }

                    #endregion
                    #region Если расширение обрабатывается, добавить файл в список для обработки

                    if (info != null)
                    {
                        FileList.Add(info);
                    }

                    #endregion

                }
                catch (Exception ex)
                {
                    if (ex.GetType() == typeof(ImageProcessingException))    // тип файла не поддерживается
                        continue;
                    Console.Out.WriteLine($"\r\n\r\nУ файла '{file}'\r\nошибка:\r\n{ex.Message}\r\n\r\n");
                    if (ex.GetType() == typeof(IOException))    // Компонент не смог прочитать файл
                        Console.Out.WriteLine($"Возможно файл испорчен, проверьте его содержимое!\r\n\r\n");
                    return 1;
                }
            }

            /*
             * Если config-файла нет или он пустой, делаем автоматические действия:
             * Смотрим time zone всех фотографий (.jpg или .heic),
             * и если у всех фотографий time zone одинаковое,
             * то устанавливаем этот time zone для всех видео,
             * поскольку в видео-файлах время в UTC и часовой пояс отсутствует.
             */
            if (Config.Items.Count == 0)
            {
                /*
                 * Идея такая: из всех фотографий, где есть часовой пояс (time zone),
                 * выстраиваем упорядоченный список по времени съемки,
                 * к каждому времени съемки прицепляем часовой пояс из фото.
                 * Потом для каждого видео, берем его время съемки и ищем
                 * в сортированном списке ближайшее по времени съемки
                 * фото - предыдущее и последующее, в каждом из них есть часовой пояс.
                 * Если часовые пояса различаются (ну вдруг), то берем ту, которая
                 * привязана к ближайшему времени съемки фото ко времени съемки видео.
                 */
                SortedList<DateTime, TimeSpan> sortedTimeZoneInfo = new SortedList<DateTime, TimeSpan>();
                bool sameZone = true;
                TimeSpan? zone = null;
                bool hasVideo = false;
                bool hasPhoto = false;
                foreach (var file in FileList)
                {
                    if (file.IsVideo == true)
                    {
                        hasVideo = true;
                        continue;
                    }
                    if (file.IsVideo == false)
                    {
                        hasPhoto = true;
                    }
                    if (file.PhotoDateTime.HasValue &&
                        file.TimeOffset.HasValue &&
                        file.TimeOffset.Value != TimeSpan.Zero &&
                        file.IsVideo == false)
                    {
                        // Ключ словаря должен составлять время съемки в часовом поясе UTC.
                        // Исключаем ситуацию с ошибкой, когда две фотографии имеют одинаковое время съемки.
                        sortedTimeZoneInfo[ file.PhotoDateTime.Value - file.TimeOffset.Value ] = file.TimeOffset.Value;

                        if (zone == null)
                            zone = file.TimeOffset;
                        sameZone &= zone == file.TimeOffset;
                    }
                }
                if (sameZone && zone.HasValue)
                {
                    Console.Out.WriteLine(
                        $"Все фотографии в одном часовом поясе " +
                        $"{(zone.Value >= TimeSpan.Zero ? "+" : "")}{zone?.ToString("hh\\:mm")}.\r\n" +
                        $"Для видео автоматически устанавливается часовой пояс по фотографиям.");

                    foreach (var file in FileList)
                    {
                        if (file.IsVideo == true &&
                            file.PhotoDateTime.HasValue &&
                            zone.HasValue)
                        {
                            // Для фото значение будет таким же, как было,
                            // для видео будет сдвиг от UTC к часовому поясу из фото.
                            file.PhotoDateTime = file.PhotoDateTime.Value + zone.Value - file.TimeOffset;
                            // Установка часового пояса для файла
                            file.TimeOffset = zone.Value;
                        }
                    }
                }
                else
                if (sortedTimeZoneInfo.Count > 0)
                {
                    Console.Out.WriteLine(
                        $"Фотографии в разных часовых поясах. Для каждого видео автоматически\r\n" +
                        $"устанавливается часовой пояс из ближайшей по времени съемки фотографии.");

                    foreach (var file in FileList)
                    {
                        if (file.IsVideo == true &&
                            file.PhotoDateTime.HasValue &&
                            zone.HasValue)
                        {
                            #region Поиск ближайшего методом деления отрезка пополам

                            // Время съемки видео также надо привести к UTC
                            DateTime aim = file.PhotoDateTime.Value - (file.TimeOffset ?? TimeSpan.Zero);
                            int minIndex = 0;
                            int maxIndex = sortedTimeZoneInfo.Count - 1;

                            // Проверка, что видео не находится раньше первой фотографии или позже последней,
                            // в таком случае не нужен цикл поиска
                            DateTime minVal = sortedTimeZoneInfo.GetKeyAtIndex(minIndex);
                            DateTime maxVal = sortedTimeZoneInfo.GetKeyAtIndex(maxIndex);
                            if (aim < minVal)
                                maxIndex = minIndex;
                            if (aim > maxVal)
                                minIndex = maxIndex;

                            while (minIndex + 1 < maxIndex)
                            {
                                int index = (minIndex + maxIndex) / 2;
                                if (index < minIndex)
                                    index = minIndex;
                                if (index > maxIndex)
                                    index = maxIndex;

                                DateTime midVal = sortedTimeZoneInfo.GetKeyAtIndex(index);

                                if (midVal > aim)
                                {
                                    maxIndex = index;
                                }
                                else
                                if (midVal < aim)
                                {
                                    minIndex = index;
                                }
                                else
                                {
                                    // Удивительно, но мы точно попали в дату и время
                                    TimeSpan targetZone = sortedTimeZoneInfo.GetValueAtIndex(index);
                                    file.PhotoDateTime = aim + targetZone;
                                    file.TimeOffset = targetZone;
                                    break;
                                }
                            }

                            // Если в списке всего одна фотография или интервал сократился
                            // до одного единственного элемента, например в ситуации, когда
                            // видео самое первое или самое последнее,
                            // берем часовой пояс из единственной фотографии.
                            if (minIndex == maxIndex)
                            {
                                TimeSpan targetZone = sortedTimeZoneInfo.GetValueAtIndex(minIndex);
                                file.PhotoDateTime = aim + targetZone;
                                file.TimeOffset = targetZone;
                            }
                            else
                            {
                                // Берем часовой пояс от той фотографии,
                                // время съемки которой ближе к съемке видео
                                minVal = sortedTimeZoneInfo.GetKeyAtIndex(minIndex);
                                maxVal = sortedTimeZoneInfo.GetKeyAtIndex(maxIndex);

                                TimeSpan targetZone =
                                    Math.Abs(aim.Ticks - minVal.Ticks) < Math.Abs(maxVal.Ticks - aim.Ticks)
                                    ? sortedTimeZoneInfo.GetValueAtIndex(minIndex)
                                    : sortedTimeZoneInfo.GetValueAtIndex(maxIndex);
                                file.PhotoDateTime = aim + targetZone;
                                file.TimeOffset = targetZone;
                            }

                            #endregion
                        }
                    }
                }
                else
                if (hasVideo && hasPhoto)
                {
                    Console.Out.WriteLine("\r\nВНИМАНИЕ!  Для видео не устанавливается часовой пояс по фотографиям!\r\n");
                }
            }

            #region Установка названия файла с учетом даты, времени и часового пояса

            for (int index = 0; index < FileList.Count; index++)
            {
                var file = FileList[ index ];
                if (!file.PhotoDateTime.HasValue)
                    continue;

                DateTime t = file.PhotoDateTime.Value;
                int maxCounter = FileList.Count + 2;
                while (maxCounter-- >= 0)
                {
                    /*
                     * Пытаемся перебором подобрать уникальное имя по дате и времени.
                     * В одну секунду может быть сделано несколько фото одним или несколькими фотоаппаратами.
                     * Мы берем исходное время и проверяем есть ли другая фотография с таким же датой и временем.
                     * Если есть, добавляем 1/10 секунды и проверяем снова.
                     * Время фотографии в названии файла может убежать, но не далеко.
                     */

                    string unique = t.ToString(options.Template);
                    if (FileList.Any(x => x.DateTimePartName == unique))
                    {
                        t = t.AddMilliseconds(100);
                    }
                    else
                    {
                        file.DateTimePartName = unique;
                        break;
                    }
                }

                if (maxCounter < 0)
                {
                    Console.Out.WriteLine("Ошибка подбора названия файла чтобы не совпадали названия файлов");
                    return 1;
                }

                file.FinalName = $"{file.DateTimePartName}{file.NameDescription}{file.Extention}";
                file.TmpName = $"{Path.GetFileNameWithoutExtension(file.OriginalName)}._tmp_{file.Extention}";
            }

            #endregion

            // Сортировка
            FileList.Sort(ExifFileInfo.Comparer);

            #region Сначала все файлы переименовываются во временные названия, чтобы не мешаться при нормальном переименовании

            Stack<Tuple<string, string>> rollback = new Stack<Tuple<string, string>>();

            for (int index = 0; index < FileList.Count; index++)
            {
                ExifFileInfo info = FileList[ index ];
                if (info.OriginalName == null || info.TmpName == null)
                    continue;
                string src = Path.Combine(workingDirectory, info.OriginalName);
                string dst = Path.Combine(workingDirectory, info.TmpName);
                try
                {
                    // Переименование файла
                    File.Move(src, dst);
                    // Сохраняем информацию для обратного переименования на случай ошибки
                    rollback.Push(new Tuple<string, string>(dst, src));
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLine(
                        $"!!!!!\r\n{ex.Message}\r\nОшибка при переименовании файла " +
                        $"из '{System.IO.Path.GetFileName(src)}' в '{System.IO.Path.GetFileName(dst)}'.\r\n" +
                        $"Автоматическое переименование всех файлов обратно!");
                    try
                    {
                        while (rollback.TryPop(out Tuple<string, string>? backRename))
                        {
                            File.Move(backRename.Item1, backRename.Item2);
                        }
                    }
                    catch (Exception ex2)
                    {
                        Console.Out.WriteLine(
                            $"!!!!!\r\n{ex2.Message}\r\nОшибка обратного переименования файлов!");
                    }
                    return 1;
                }
            }

            #endregion
            #region Потом делается нормальное переименование из временных названий

            for (int index = 0; index < FileList.Count; index++)
            {
                ExifFileInfo info = FileList[ index ];

                if (info.FinalName == null || info.TmpName == null || info.PhotoDateTime == null)
                    continue;

                try
                {
                    string src = Path.Combine(workingDirectory, info.TmpName);
                    string dst = Path.Combine(workingDirectory, info.FinalName);
                    try
                    {
                        FileInfo fInfo = new FileInfo(src);
                        fInfo.MoveTo(dst);
                        fInfo.CreationTime = info.PhotoDateTime.Value;
                        fInfo.LastWriteTime = info.PhotoDateTime.Value;

                        // Сохраняем информацию для обратного переименования на случай ошибки
                        rollback.Push(new Tuple<string, string>(dst, src));
                    }
                    catch (Exception ex)
                    {
                        Console.Out.WriteLine(
                            $"!!!!!\r\n{ex.Message}\r\n" +
                            $"Ошибка при переименовании файла " +
                            $"из '{System.IO.Path.GetFileName(src)}' в '{System.IO.Path.GetFileName(dst)}'.\r\n" +
                            $"Автоматическое переименование всех файлов обратно!");
                        try
                        {
                            while (rollback.TryPop(out Tuple<string, string>? backRename))
                            {
                                File.Move(backRename.Item1, backRename.Item2);
                            }
                        }
                        catch (Exception ex2)
                        {
                            Console.Out.WriteLine(
                                $"!!!!!\r\n{ex2.Message}\r\nОшибка обратного переименования файлов!");
                        }
                        return 1;
                    }
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLine("Ошибка:\r\n\r\n{0}\r\n\r\n", ex.Message);
                }
            }
            Console.Out.WriteLine("Обработано файлов: {0}\r\n", FileList.Count);

            #endregion

            return 0;
        }

        #endregion
    }
}
