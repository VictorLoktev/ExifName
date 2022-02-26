using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExifName
{
	public class ExifFileInfo
	{
		public string	OriginalName;
		public string	FinalName;
		public string	TmpName;
		public string	Extention;
		public string	NameWithoutExtention;
		public string	NameDescription;
		public DateTime	PhotoDateTime;
		public int		IncrementNumber;
		public string	CameraOwner;
		public string	CameraInternalSerialNumber;
		public string	CameraModel;
		public string	CameraMake;
		public TimeSpan	TimeOffset;

		public ExifFileInfo()
		{
			PhotoDateTime = DateTime.MinValue;
			TimeOffset = new TimeSpan( 0, 0, 0 );
		}

		public static int Comparer( ExifFileInfo a, ExifFileInfo b )
		{
			int i;
			i = DateTime.Compare( a.PhotoDateTime, b.PhotoDateTime );
			if( i != 0 ) return i;
			i = String.Compare( a.OriginalName, b.OriginalName );
			if( i != 0 ) return i;
			i = String.Compare( a.FinalName, b.FinalName );
			return i;
		}
	}

}
