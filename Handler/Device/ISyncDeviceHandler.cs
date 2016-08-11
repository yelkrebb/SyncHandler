using System;
using Motion.Mobile.Core.BLE;

namespace Motion.Core.SyncHandler
{
	public interface ISyncDeviceHandler
	{
		void SetAdapter(IAdapter adapter);

		void SetDevice(IDevice device);

		void StartSync();

		ICharacteristic GetServicesCharacterisitic(Constants.CharacteristicsUUID uuid);

		void ProcessCommands();

		void CleanUp();
	}
}

