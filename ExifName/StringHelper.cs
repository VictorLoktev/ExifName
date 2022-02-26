using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExifName
{
	public static class StringHelper
	{
		public static string Reverse( this string s )
		{
			char[] charArray = s.ToCharArray();
			Array.Reverse( charArray );
			return new string( charArray );
		}

		public static bool ContainsIgnoreCase( this string s, string stringToFind )
		{
			return System.Globalization.CultureInfo.CurrentCulture.CompareInfo.IndexOf( s, stringToFind, System.Globalization.CompareOptions.IgnoreCase ) >= 0;
		}
	}
}