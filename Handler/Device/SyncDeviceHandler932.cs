using System;
using System.Diagnostics;
using Motion.Mobile.Core.BLE;

namespace Motion.Core.SyncHandler
{
<<<<<<< HEAD
	public class SyncDeviceHandler932
	{


=======
	public class SyncDeviceHandler932 : ISyncDeviceHandler
	{
		private static readonly object _synclock = new object();
		private static SyncDeviceHandler932 Instance;

		private IAdapter Adapter;

		public SyncDeviceHandler932()
		{
		}

		public static SyncDeviceHandler932 GetInstance()
		{
			if (Instance == null)
			{
				lock (_synclock)
				{
					if (Instance == null)
					{
						Instance = new SyncDeviceHandler932();
					}
				}
			}

			return Instance;
		}

		public void CleanUp()
		{
			throw new NotImplementedException();
		}

		public void StartSync()
		{
			Debug.WriteLine("SyncDeviceHandler932: Start syncing...");
		}

		public void ProcessCommands()
		{
			throw new NotImplementedException();
		}

		public void SetAdapter(IAdapter adapter)
		{
			this.Adapter = adapter;
		}

		public void SetDevice(IDevice device)
		{
			throw new NotImplementedException();
		}

		public void GetServicesCharacterisitic(Constants.CharacteristicsUUID uuid)
		{
			throw new NotImplementedException();
		}

		ICharacteristic ISyncDeviceHandler.GetServicesCharacterisitic(Constants.CharacteristicsUUID uuid)
		{
			throw new NotImplementedException();
		}

		public void GetValueFromChar(object sender, CharacteristicReadEventArgs e)
		{
			throw new NotImplementedException();
		}
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
	}
}

