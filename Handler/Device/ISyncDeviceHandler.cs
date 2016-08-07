using System;
using Motion.Mobile.Core.BLE;

namespace Motion.Core.SyncHandler
{
	public interface ISyncDeviceHandler
	{
		void SetAdapter(IAdapter adapter);

		void SetDevice(IDevice device);

		void StartSync();

		void GetValueFromChar(object sender, CharacteristicReadEventArgs e);

		ICharacteristic GetServicesCharacterisitic(Constants.CharacteristicsUUID uuid);

		void ProcessCommands();

		void CleanUp();
	}
}

