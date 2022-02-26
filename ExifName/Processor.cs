﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using MetadataExtractor.Util;

namespace ExifName
{
	public class Processor
	{
		#region Константы и переменные

		const string _tmp_ = "._tmp_";
		const string ConfigFileName = "exifname.config";
		const string ConfigRegex = @"^\s*(?<sign>[+-])(?<time>\d\d?:\d\d?(:\d\d?(.\d+)?)?)\s*:\s*(?<camera>.+)\s*$";

		public struct ConfigInfo
		{
			public TimeSpan TimeShift;
			public string Camera;

			public ConfigInfo( TimeSpan timeShift, string camera )
			{
				TimeShift = timeShift;
				Camera = camera;
			}
		}

		#endregion

		public void Run( string[] args )
		{
			List<ConfigInfo> config = new List<ConfigInfo>();

			try
			{
				#region Параметр

				string srcDir = Environment.CurrentDirectory;
				if( args.Length > 0 )
				{
					srcDir = args[ 0 ];
					// Если параметром указан файл, а не директория, то по файлу выдается вся информация из EXIF
					if( System.IO.File.Exists( args[ 0 ] ) )
					{
						Console.WriteLine( "Полная информация о файле:" );
						var directories = ImageMetadataReader.ReadMetadata( args[ 0 ] );
						foreach( var directory in directories )
						{
							Console.WriteLine( directory.Name );
							foreach( var tag in directory.Tags )
							{
								bool simple = false;
								if( directory.GetType() == typeof( ExifIfd0Directory ) )
								{
									switch( tag.Type )
									{
									case ExifIfd0Directory.TagModel:
									case ExifIfd0Directory.TagMake:
									case ExifIfd0Directory.TagBodySerialNumber:
										Console.WriteLine( $"{tag}     => CAMERA in config" );
										break;
									case ExifIfd0Directory.TagCameraOwnerName:
										Console.WriteLine( $"{tag}     => OWNER in config" );
										break;
									case ExifIfd0Directory.TagDateTime:
										Console.WriteLine( $"{tag}     => DATE+TIME priority 1" );
										break;
									default:
										simple = true;
										break;
									}
								}
								else
								if( directory.GetType() == typeof( ExifSubIfdDirectory ) )
								{
									switch( tag.Type )
									{
									case ExifSubIfdDirectory.TagDateTimeOriginal:
										Console.WriteLine( $"{tag}     => DATE+TIME priority 2" );
										break;
									case ExifSubIfdDirectory.TagDateTimeDigitized:
										Console.WriteLine( $"{tag}     => DATE+TIME priority 3" );
										break;
									default:
										simple = true;
										break;
									}
								}
								else
								if( directory.GetType() == typeof( QuickTimeMovieHeaderDirectory ) )
								{
									switch( tag.Type )
									{
									case QuickTimeMovieHeaderDirectory.TagCreated:
										Console.WriteLine( $"{tag}     => DATE+TIME priority 4" );
										simple = false;
										break;
									default:
										simple = true;
										break;
									}
								}
								else
								{
									simple = true;
								}
								if( simple )
									Console.WriteLine( tag );
							}
						}
						return;
					}
				}
				else
					Console.Out.WriteLine( "В параметре не задан путь к обрабатываемой директории, используется текущая." );

				#endregion
				#region Чтение файла конфигурации

				// Единый конфигурационный файл программы в папке с программой
				//ExifConfigurationSection config = ConfigurationManager.GetSection( "exif" ) as ExifConfigurationSection;
				//if( config == null )
				//{
				//	Console.Out.WriteLine( "Отсутствует конфигурационный файл или в конфигурационном файле не задана секция " );
				//	return;
				//}

				// Конфигурационный файл в обрабатываемой папке
				string configFile = Path.Combine( srcDir, ConfigFileName );
				if( File.Exists( configFile ) )
				{
					Regex r = new Regex( ConfigRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase );
					string[] configLines = File.ReadAllLines( configFile );
					bool emptyConfig = true;
					for( int line = 0; line < configLines.Length; line++ )
					{
						string configLine = configLines[ line ].Trim();
						if( string.IsNullOrWhiteSpace( configLine ) ) continue;
						emptyConfig = false;
						if( configLine.StartsWith( "//" ) ||
							configLine.StartsWith( "#" ) ||
							configLine.StartsWith( "--" ) ||
							configLine.StartsWith( "REM" ) ) continue; // комментарий

						TimeSpan span;
						Match m = r.Match( configLine );
						if( !m.Success ||
							!m.Groups[ "sign" ].Success ||
							!m.Groups[ "time" ].Success ||
							!m.Groups[ "camera" ].Success ||
							!TimeSpan.TryParse( m.Groups[ "time" ].Value, out span ) )
						{
							Console.WriteLine(
								$"Нераспознанный синтаксис строки {line + 1} конфигурационного файла папки.\r\n" +
								"#    Полный формат строки конфигурации:\r\n" +
								"#    <знак>HH:MM[:SS[.ttt]] : <подстрока CAMERA>\r\n" +
								"#    <знак> Обязательный!\r\n"
								);
							return;
						}
						string camera = m.Groups[ "camera" ].Value?.Trim();
						config.Add( new ConfigInfo( span, camera ) );
					}
					if( emptyConfig )
					{
						File.WriteAllText( configFile,
							"# Конфигурационный файл программы ExifName.\r\n" +
							"# Действие конфигурационного файла распространяется на все файлы данной папки.\r\n" +
							"# Формат файла:\r\n" +
							"# 1) Символы #, -- или // в начале строки указывают на комментарий.\r\n" +
							"# 2) В строке задается временная зона (смещение) для конкретного фотоаппарата,\r\n" +
							"#    фотоаппарат задается подстрокой параметра CAMERA\r\n" +
							"#    (см. exif-информацию, выдаваемую программой по файлу вызовом ExifName <filename>).\r\n" +
							"#    Смещение временой зоны записывается в формате <знак>HH:MM[:SS[.ttt]],\r\n" +
							"#    где <знак> один из символов: + или -.\r\n" +
							"#    Если знак не задан, будет ошибка.\r\n" +
							"#    Полный формат строки конфигурации:\r\n" +
							"#    <знак>HH:MM[:SS[.ttt]] : <подстрока CAMERA>\r\n" +
							"#\r\n" +
							"#    Видео файлы от телефонов не содержат информации о телефоне чтобы разделить файлы по аппаратам!\r\n" +
							"#    Для видео-файлов следует указывать владельца \"VIDEDO\".\r\n" +
							"#    Допустимо указывать смещение для одной камеры, а затем общее cмещение без указания названия камеры,\r\n" +
							"#    Последнее будет использовано для видео файлов и всех, кто не подпаадет под указаное название\r\n" +
							"# \r\n" +
							"# +03:00 : SAMSUNG\r\n" +
							"# +03:00 : OLIMPUS\r\n" +
							"#\r\n" +
							"#\r\n\r\n\r\n"
							);
						Console.WriteLine( $"Конфигурационный файл {ConfigFileName} в папке заполнен комментарием." );
					}
				}

				#endregion

				Console.Out.WriteLine( "Обрабатывается директория:" );
				Console.Out.WriteLine( srcDir );

				/*
				Отлавливаем в названиях файлов два варианта конструкции:
				Первый - буквы и цыфры до пробела, пробел здесь разделитель части нумерации файла и комментария в названии.
				Второй для файлов вида "Картинка 1.jpg".
				Поэтому к номерной части относим все, включая пробел, после которого цифра, а пробкл, после которого нет цифры относим к комментарию.

				https://stackoverflow.com/questions/28156769/foreign-language-characters-in-regular-expression-in-c-sharp
				*/

				Regex rx = new Regex(
					//@"^(?:(DSC|IMG|PIC|P|PA|PB|PC|Photo|Video)[_-]?\d+|(?:[0-9]|(?:-|_)[0-9])+)(?'number_in_brackets'\(\d+\))?(?'name'\s*-\s*.*|\D\D.*)?$",
					@"^(?'name'(?:\p{L}|[()_]|\d| \d|-|(<!- ))+)(?'comment'.*)$",
					RegexOptions.Compiled | RegexOptions.IgnoreCase );

				string guid = Guid.NewGuid().ToString( "N" );
				string undoFile1 = Path.Combine( System.IO.Path.GetTempPath(),
					$"exifname undo {DateTime.Now.ToString( "yyyy-MM-dd hh-mm-ss" )} {guid} run 1st.cmd" );
				string undoFile2 = System.IO.Path.Combine( System.IO.Path.GetTempPath(),
					$"exifname undo {DateTime.Now.ToString( "yyyy-MM-dd hh-mm-ss" )} {guid} run 2nd.cmd" );

				List<ExifFileInfo> FileList = new List<ExifFileInfo>();
				string[] files = System.IO.Directory.GetFiles( srcDir, "*", System.IO.SearchOption.TopDirectoryOnly );
				foreach( string file in files )
				{
					try
					{
						var directories = ImageMetadataReader.ReadMetadata( file );

						#region Чтение информации о файле

						ExifFileInfo info = new ExifFileInfo();

						// Название файла
						info.OriginalName = file;
						info.Extention = System.IO.Path.GetExtension( info.OriginalName ).ToLower();
						info.NameWithoutExtention = System.IO.Path.GetFileNameWithoutExtension( info.OriginalName );
						info.NameDescription = "";

						bool dateInExifFound = false;

						var pic0Exif = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
						var picSubExif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
						var movExif = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();

						if( pic0Exif != null )
						{
							info.CameraModel = pic0Exif.GetString( ExifDirectoryBase.TagModel );
							info.CameraMake = pic0Exif.GetString( ExifDirectoryBase.TagMake );
							info.CameraInternalSerialNumber = pic0Exif.GetString( ExifDirectoryBase.TagBodySerialNumber );
							info.CameraOwner = pic0Exif.GetString( ExifDirectoryBase.TagCameraOwnerName );

							if( pic0Exif.TryGetDateTime( ExifSubIfdDirectory.TagDateTime, out var datetime ) )
							{
								info.PhotoDateTime = datetime;
								dateInExifFound = true;
							}
						}
						if( picSubExif != null )
						{
							if( dateInExifFound && picSubExif.TryGetInt32( ExifSubIfdDirectory.TagSubsecondTime, out var subSec ) )
							{
								//[Exif SubIFD] Sub-Sec Time - 0854
								// Субсекундная часть иногда записывается сюда
								info.PhotoDateTime += new TimeSpan( 0, 0, 0, 0, subSec );
							}
							if( !dateInExifFound && picSubExif.TryGetDateTime( ExifSubIfdDirectory.TagDateTimeOriginal, out var datetime ) )
							{
								info.PhotoDateTime = datetime;
								dateInExifFound = true;
								if( picSubExif.TryGetInt32( ExifSubIfdDirectory.TagSubsecondTimeOriginal, out subSec ) )
								{
									//[Exif SubIFD] Sub-Sec Time Original - 0854
									// Субсекундная часть иногда записывается сюда
									info.PhotoDateTime += new TimeSpan( 0, 0, 0, 0, subSec );
								}
							}

							if( !dateInExifFound && picSubExif.TryGetDateTime( ExifSubIfdDirectory.TagDateTimeDigitized, out datetime ) )
							{
								info.PhotoDateTime = datetime;
								dateInExifFound = true;
								if( picSubExif.TryGetInt32( ExifSubIfdDirectory.TagSubsecondTimeDigitized, out subSec ) )
								{
									//[Exif SubIFD] Sub-Sec Time Digitized - 0854
									// Субсекундная часть иногда записывается сюда
									info.PhotoDateTime += new TimeSpan( 0, 0, 0, 0, subSec );
								}
							}
						}
						if( dateInExifFound )
						{
							ConfigInfo? inf = null;
							if( !inf.HasValue && !string.IsNullOrEmpty( info.CameraInternalSerialNumber ) )
								FindCameraConfig( config, info.CameraInternalSerialNumber );
							if( !inf.HasValue && !string.IsNullOrEmpty( info.CameraOwner ) )
								inf = FindCameraConfig( config, info.CameraOwner );
							if( !inf.HasValue && !string.IsNullOrEmpty( info.CameraModel ) )
								inf = FindCameraConfig( config, info.CameraModel );
							if( !inf.HasValue && !string.IsNullOrEmpty( info.CameraMake ) )
								inf = FindCameraConfig( config, info.CameraMake );

							if( inf.HasValue )
							{
								// Из времени файла в exif вычитается сдвиг зоны конфигурационного файла - это приведение к GMT,
								// а затем добавляется смещение относительно GMT времени файла чтобы получить текущую зону дома владельца
								System.IO.FileInfo fInfo = new System.IO.FileInfo( file );
								DateTime d1 = fInfo.LastWriteTimeUtc;
								DateTime d2 = fInfo.LastWriteTime;
								info.PhotoDateTime +=
									- inf.Value.TimeShift
									+ d2.Subtract( d1 );
							}
							else
							if( config.Count > 0 )
							{
								Console.WriteLine(
									"В конфигурационном файле не задана камера:\r\n" +
									$"для файла: {info.OriginalName}\r\n" +
									$"Серийный номер: {info.CameraInternalSerialNumber}\r\n" +
									$"Владелец: {info.CameraOwner}\r\n" +
									$"Модель: {info.CameraModel}\r\n" +
									$"Производитель: {info.CameraMake}\r\n"
									);
								return;
							}
						}
						if( !dateInExifFound && movExif != null )
						{
							foreach( int tag in new int[] { QuickTimeMovieHeaderDirectory.TagCreated } )
							{
								if( movExif.TryGetDateTime( tag, out var datetime ) )
								{
									TimeSpan currentOffset = new TimeSpan();
									var major = directories.OfType<QuickTimeFileTypeDirectory>().FirstOrDefault();
									if( major != null )
									{
										switch( ( major.GetString( QuickTimeFileTypeDirectory.TagMajorBrand ) ?? "" ).ToLower().Trim() )
										{
										// смещение не нужно
										case "3gp5":// Телефон типа HTC Hero
										case "qt":  // QuickTime
											currentOffset = new TimeSpan();
											break;
										// Смещение нужно
										case "isom":// OLYMPUS E-P5
										case "mp42":// Samsung Galaxy 
										case "3gp":// Телефон
										case "3gp2":// Телефон
										case "3gp3":// Телефон
										case "3gp4":// Телефон типа HTC Hero
													//currentOffset = TimeZone.CurrentTimeZone.GetUtcOffset( DateTime.Now );
											System.IO.FileInfo fInfo = new System.IO.FileInfo( file );
											DateTime d1 = fInfo.LastWriteTimeUtc;
											DateTime d2 = fInfo.LastWriteTime;
											currentOffset = d2.Subtract( d1 );
											break;
										default:
											Console.Error.WriteLine( $"Файл `{info.NameWithoutExtention}{info.Extention}' " +
												$"содержит неизвестный Major Brand `{( major.GetString( QuickTimeFileTypeDirectory.TagMajorBrand ) ?? "" )}'" );
											break;
										}
									}
									ConfigInfo? inf = FindCameraConfig( config, "VIDEO" );
									if( !inf.HasValue )
										inf = FindCameraConfig( config, "" );
									if( config.Count > 0 && !inf.HasValue )
									{
										Console.WriteLine( "В конфигурационном файле не задана камера \"VIDEO\"" );
										return;
									}

									info.PhotoDateTime = datetime + ( inf.HasValue ? inf.Value.TimeShift : currentOffset );
									dateInExifFound = true;
									break;
								}
							}
						}
						// Если в файле нет даты в EXIF, он пропускается
						if( !dateInExifFound )
							continue;

						// Защита от сброшенных в фотоаппарате дат
						if( info.PhotoDateTime < new DateTime( 2001, 3, 1 ) || info.PhotoDateTime > DateTime.Now ||
							info.PhotoDateTime == new DateTime( 2004, 1, 1, 0, 0, 0 ) )
						{
							Console.Error.WriteLine( $"Файл `{info.NameWithoutExtention}{info.Extention}' " +
								$"содержит неправильную дату съемки `{info.PhotoDateTime.ToString( "dd.MM.yyyy HH:mm:ss" )}'" );
							continue;
						}

						#endregion

						#region Если названию файла уже доблено текстовое название

						if( info != null )
						{
							// Попытка вычленить комментарий к фотографии в названии файла.
							Match m = rx.Match( info.NameWithoutExtention );
							if( m.Success )
							{
								info.NameDescription = m.Groups[ "comment" ]?.Value;
							}
							else
							{
								// Установить дату и время файла из Exif
								System.IO.FileInfo fInfo = new System.IO.FileInfo( System.IO.Path.Combine( srcDir, info.OriginalName ) );
								File.SetCreationTime( fInfo.FullName, info.PhotoDateTime );
								File.SetLastWriteTime( fInfo.FullName, info.PhotoDateTime );
								// и не переименовывать
								info = null;
							}
						}

						#endregion
						#region Если расширение обрабатыватся, добавить файл в список для обработки и назначение имени

						if( info != null )
						{
							info.FinalName = string.Format( "{0}{1}{2}{3}",
								info.PhotoDateTime.ToString( "yyyy-MM-dd-HHmm" ),
								"{IncrementNumber3}",
								info.NameDescription,
								info.Extention
								);
							info.TmpName = string.Format( "{0}" + _tmp_ + "{1}",
								System.IO.Path.GetFileNameWithoutExtension( info.OriginalName ),
								info.Extention
								);
							FileList.Add( info );
						}

						#endregion

					}
					catch( Exception ex )
					{
						if( ex.GetType() == typeof( ImageProcessingException ) )    // тип файла не поддерживается
							continue;
						Console.Out.WriteLine( $"\r\n\r\nУ файла `{file}'\r\nошибка:\r\n«{ex.Message}»\r\n\r\n" );
						if( ex.GetType() == typeof( System.IO.IOException ) )    // Компонент не смог прочитать файл
							Console.Out.WriteLine( $"Возможно файл битый, проверьте его содержимое!\r\n\r\n" );
						return;
					}
				}
				// Сортировка
				FileList.Sort( ExifFileInfo.Comparer );

				#region Сколько цифр в сквозном нумераторе

				string incFormat = "D3";
				if( FileList.Count.ToString( "G" ).Length > 3 )
					incFormat = "D" + FileList.Count.ToString( "G" ).Length;

				#endregion
				#region Сначала все файлы переименовываются во временные названия, чтобы не мешаться при нормальном переименовании

				for( int index = 0; index < FileList.Count; index++ )
				{
					ExifFileInfo info = FileList[ index ];
					string src = System.IO.Path.Combine( srcDir, info.OriginalName );
					string dst = System.IO.Path.Combine( srcDir, info.TmpName );
					try
					{
						// В undo файл в temp'е записывает командный файл обратного переименования
						System.IO.File.AppendAllText( undoFile2, $"ren \"{dst}\" \"{System.IO.Path.GetFileName( src )}\"\r\n", Console.InputEncoding );
					}
					catch( Exception ex )
					{
						Console.Out.WriteLine( $"Ошибка\r\n«{ex.Message}»\r\nпри записи в файл undo\r\n{undoFile2}\r\n" );
						return;
					}
					try
					{
						// Переименование файла
						System.IO.File.Move( src, dst );
					}
					catch( Exception ex )
					{
						Console.Out.WriteLine( $"Ошибка\r\n«{ex.Message}»\r\nпри переименовании файла из\r\n{src}\r\nв\r\n{dst}" );
						return;
					}
				}

				#endregion
				#region Потом делается нормальное переименование из временных названий

				for( int index = 0; index < FileList.Count; index++ )
				{
					ExifFileInfo info = FileList[ index ];
					info.IncrementNumber = index;   // расстановка сквозного нумератора по файлам в директории

					// проверка что файла с таким названием еще нет, иначе нумератор увеличивается на 1
					int maxN = 10000;
					string fileName = "";
					do
					{
						info.IncrementNumber++;
						fileName = info.FinalName.Replace( "{IncrementNumber3}", info.IncrementNumber.ToString( incFormat ) );
						maxN--;
					}
					while( System.IO.File.Exists( System.IO.Path.Combine( srcDir, fileName ) ) && maxN > 0 );

					info.FinalName = fileName;
					try
					{
						string src = System.IO.Path.Combine( srcDir, info.TmpName );
						string dst = System.IO.Path.Combine( srcDir, info.FinalName );
						try
						{
							// В undo файл в temp'е записывает командный файл обратного переименования
							System.IO.File.AppendAllText( undoFile1, $"ren \"{dst}\" \"{System.IO.Path.GetFileName( src )}\"\r\n", Console.InputEncoding );
						}
						catch( Exception ex )
						{
							Console.Out.WriteLine( $"Ошибка\r\n«{ex.Message}»\r\nпри записи в файл undo\r\n{undoFile1}\r\n" );
							return;
						}

						try
						{
							System.IO.FileInfo fInfo = new System.IO.FileInfo( src );
							fInfo.MoveTo( dst );
							fInfo.CreationTime = info.PhotoDateTime;
							fInfo.LastWriteTime = info.PhotoDateTime;
						}
						catch( Exception ex )
						{
							Console.Out.WriteLine( $"Ошибка\r\n«{ex.Message}»\r\nпри смене названия у файла\r\n{dst}\r\n" );
							return;
						}
					}
					catch( Exception ex )
					{
						Console.Out.WriteLine( "Ошибка:\r\n\r\n{0}\r\n\r\n", ex.Message );
					}
				}
				Console.Out.WriteLine( "Обработано файлов: {0}\r\n", FileList.Count );

				#endregion
			}
			catch( Exception ex )
			{
				Console.Out.WriteLine( "Ошибка:\r\n\r\n{0}\r\n\r\n", ex.Message );
			}
		}
		private ConfigInfo? FindCameraConfig( List<ConfigInfo> config, string camera )
		{
			foreach( ConfigInfo inf in config )
			{
				if( string.IsNullOrEmpty( inf.Camera ) ||
					( camera ?? "" ).ContainsIgnoreCase( inf.Camera ) )
					return inf;
			}
			return null;
		}
	}
}
