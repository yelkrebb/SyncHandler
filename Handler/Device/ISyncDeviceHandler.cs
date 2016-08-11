using System;
using Motion.Mobile.Core.BLE;

namespace Motion.Core.SyncHandler
{
	public interface ISyncDeviceHandler
	{
		void SetAdapter(IAdapter adapter);

		void SetDevice(IDevice device);

		void StartSync();

<<<<<<< HEAD
=======
		void GetValueFromChar(object sender, CharacteristicReadEventArgs e);

>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
		ICharacteristic GetServicesCharacterisitic(Constants.CharacteristicsUUID uuid);

		void ProcessCommands();

		void CleanUp();
	}
}

