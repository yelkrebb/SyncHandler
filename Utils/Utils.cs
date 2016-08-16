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

		public static String ByteArrayToHexString(byte[] data)
		{
			string hex = BitConverter.ToString(data);
			return hex.Replace("-", "");
		}

		public static bool TerminatorFound(byte terminatorChar, int terminatorLength, byte[] data)
		{
			bool found = false;
			int count = 0;

			foreach(byte b in data) {
				if (b == terminatorChar)
				{
					count++;
				}
				if (count >= terminatorLength)
				{
					found = true;
					break;
				}

			}

			return found;
		}

		public static bool LastPacketReceived(int packetIndex,byte[] data)
		{
			bool lastPacket = false;

			if (data[packetIndex] == 00)
			{
				lastPacket = true;
			}

			return lastPacket;
		}

	}
}

