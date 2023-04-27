namespace ExifName
{
	public class ExifFileInfo
	{
		public string? OriginalName;
		public string? FinalName;
		public string? TmpName;
		public string? Extention;
		public string? NameWithoutExtention;
		public string? NameDescription;
		public int IncrementNumber;
		public string? CameraOwner;
		public string? CameraInternalSerialNumber;
		public string? CameraModel;
		public string? CameraMaker;
		public DateTime? PhotoDateTime;
		public TimeSpan? TimeOffset;
		public bool? IsVideo;

		public static int Comparer(ExifFileInfo a, ExifFileInfo b)
		{
			int i;
			i = DateTime.Compare(a.PhotoDateTime ?? DateTime.MinValue, b.PhotoDateTime ?? DateTime.MinValue);
			if (i != 0)
				return i;

			i = TimeSpan.Compare(a.TimeOffset ?? TimeSpan.MinValue, b.TimeOffset ?? TimeSpan.MinValue);
			if (i != 0)
				return i;

			i = string.Compare(a.OriginalName, b.OriginalName);
			if (i != 0)
				return i;

			i = string.Compare(a.FinalName, b.FinalName);
			return i;
		}
	}
}
