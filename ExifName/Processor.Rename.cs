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

            string guid = Guid.NewGuid().ToString("N");
            string undoFile1 = Path.Combine(Path.GetTempPath(),
                $"exifname undo {DateTime.Now:yyyy-MM-dd HH-mm-ss} {guid} run 1st.cmd");
            string undoFile2 = Path.Combine(Path.GetTempPath(),
                $"exifname undo {DateTime.Now:yyyy-MM-dd HH-mm-ss} {guid} run 2nd.cmd");

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
                                         * Для данного типа видео в файл время съемки пишется в зоне GMT.
                                         * Либо должен быть конфигурационный файл с установленным смещением по временной зоне
                                         * либо должны быть фотографии в папке, тогда по зоне из них будет делаться смещение для видео.
                                         * Если нет ни того, ни другого, смещение задается зоной по дате из файла, когда файл
                                         * переписывается с камеры/телефона на диск ПК, обычно это зона на ПК в момент копирования файла.
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
                            info.PhotoDateTime = null;
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
                            info.PhotoDateTime = null;
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
                                $"Файл '{info.NameWithoutExtention}{info.Extention}'\r\n" +
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
                        //else
                        //{
                        //	// Установить дату и время файла из Exif
                        //	FileInfo fInfo = new FileInfo( Path.Combine( srcDir, info.OriginalName ) );
                        //	File.SetCreationTime( fInfo.FullName, info.PhotoDateTime );
                        //	File.SetLastWriteTime( fInfo.FullName, info.PhotoDateTime );
                        //	// и не переименовывать
                        //	info = null;
                        //}
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
             * Смотрим time zone всех фотографий (.jpg или .heic), если оно одинаковое,
             * то задаем такой же time zone для всех видео.
             */
            if (Config.Items.Count == 0)
            {
                bool sameZone = true;
                TimeSpan? zone = null;
                foreach (var file in FileList)
                {
                    if (file.TimeOffset.HasValue &&
                        file.TimeOffset.Value != TimeSpan.Zero &&
                        file.IsVideo == false)
                    {
                        if (zone == null)
                            zone = file.TimeOffset;
                        sameZone &= zone == file.TimeOffset;
                    }
                }
                if (sameZone && zone.HasValue)
                {
                    Console.Out.WriteLine($"Для видео задается автоматическая временная зона по фотографиям: {zone?.ToString("hh\\:mm")}");

                    foreach (var file in FileList)
                    {
                        if (file.PhotoDateTime.HasValue && zone.HasValue)
                        {
                            // Для фото значение будет таким же, как было,
                            // для видео будет сдвиг от GMT к зоне из фото.
                            file.PhotoDateTime = file.PhotoDateTime.Value + zone.Value - file.TimeOffset;
                            // Установка зоны для файла
                            file.TimeOffset = zone.Value;
                        }
                    }
                }
            }

            #region Установка названия файла с учетом даты, времени и зоны

            for (int index = 0; index < FileList.Count; index++)
            {
                var file = FileList[ index ];
                if (!file.PhotoDateTime.HasValue)
                    continue;

                DateTime t = file.PhotoDateTime.Value;
                int maxCounter = FileList.Count + 2;
                while(maxCounter-->=0)
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
