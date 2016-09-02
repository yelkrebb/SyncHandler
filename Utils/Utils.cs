using System;
namespace Motion.Core.SyncHandler
{
	public class Utils
	{
		private static Random random;
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
			    advertiseName.Contains("0002001668"))
			    //advertiseName.Contains("1134679"))
			{
				result = true;
			}

			if (advertiseName.StartsWith("H25FE2"))
			{
				result = true;
			}
			return result;
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
				else {
					count = 0;
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

		public static byte[] GetRandomDigits(int length)
		{
			if (random == null) random = new Random();

			byte[] generated = new byte[length];
			for (int i = 0; i < length; i++)
			{
				byte gen = (byte) (random.Next(0, 9) + 48 );
				generated[i] = gen;
			}
			return generated;
		}

		public static bool ValidateActivationCode(String generated, String entered)
		{
			int result = 0;
			String.Compare(generated, entered, StringComparison.Ordinal);
			return (result > 0 ? false : true);
		}

	}
}

