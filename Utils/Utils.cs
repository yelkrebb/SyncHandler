using System;
namespace Motion.Core.SyncHandler
{
	public class Utils
	{
		public Utils()
		{
		}

		public static bool isValidDevice(String advertiseName)
		{
			bool result = false;
			advertiseName = advertiseName.Replace("PE", "").Replace("FT", "");
			if (/*advertiseName.StartsWith("932") ||
				advertiseName.StartsWith("936") ||
				advertiseName.StartsWith("939") ||*/
			    advertiseName.StartsWith("961"))
			{
				result = true;
			}
			return result;
		}

	}
}

