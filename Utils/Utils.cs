using System;
namespace Motion.Core.SyncHandler
{
	public class Utils
	{
		//TRIO SETTINGS UPDATE FLAG BIT LOCATION
		public const int CLEAR_EX_BIT_LOC = 0;
		public const int USER_SETTINGS_BIT_LOC = 1;
		public const int DEVICE_SETTINGS_BIT_LOC = 2;
		public const int EXERCISE_SETTINGS_BIT_LOC = 3;
		public const int COMPANY_SETTINGS_BIT_LOC = 4;
		public const int SIGNATURE_SETTINGS_BIT_LOC = 5;
		public const int SEIZURE_SETTINGS_BIT_LOC = 6;
		public const int SCREEN_SETTINGS_BIT_LOC = 7;
		public const int SENSITIVITY_SETTINGS_BIT_LOC = 8;
		public const int DEVICE_PAIRING_STAT_BIT_LOC = 9;
		public const int MESSAGE_SETTINGS_BIT_LOC = 10;
		public const int CLEAR_MESSAGES_SETTINGS_BIT_LOC = 11;
		public const int STEP_COUNTING_PARAM_SETTINGS_BIT_LOC = 12;

		//TRIO SETTINGS UPDATE FLAGS
		public const int CLEAR_EX = 0x01;
		public const int USER_SETTINGS = 0x02;
		public const int DEVICE_SETTINGS = 0x04;
		public const int EXERCISE_SETTINGS = 0x08;
		public const int COMPANY_SETTINGS = 0x10;
		public const int SIGNATURE_SETTINGS = 0x20;
		public const int SEIZURE_SETTINGS = 0x40;
		public const int SCREEN_SETTINGS = 0x80;
		public const int SENSITIVITY_SETTINGS = 0x100;
		public const int PAIRING_SETTINGS = 0x200;
		public const int MESSAGE_SETTINGS = 0x400;
		public const int CLEAR_MESSAGES_SETTINGS = 0x800;
		public const int STEP_COUNTING_PARAM_SETTINGS = 0x1000;

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

