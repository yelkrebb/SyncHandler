using System;
using Motion.Core.WSHandler;
using Motion.Mobile.Core.BLE;
using Motion.Mobile.Utilities;
using Motion.Core.Data.UserData;

namespace Motion.Core.SyncHandler
{
	public interface ISyncDeviceHandler
	{

		event EventHandler IncrementProgressBar;
		event EventHandler<SyncDoneEventArgs> SyncDone;


		void SetAdapter(IAdapter adapter);

		void SetDevice(IDevice device);

		void SetWebService(IWebServicesWrapper webservice);

		void StartSync(Constants.ScanType scanType);

		ICharacteristic GetServicesCharacteristic(Constants.CharacteristicsUUID uuid);

		void NotifyStateUpdateDone(object sender, CharacteristicReadEventArgs e);

		void ReceiveResponse(object sender, CommandResponseEventArgs e);

		void StartWriteSettings();

		void ProcessCommands();

		bool ValidateActivationCode(String enteredCode);

		void CleanUp();

		void SetUserInfo(UserInformation userInfo);

	}
}

