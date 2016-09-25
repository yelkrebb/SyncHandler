using System;
using Motion.Core.WSHandler;
using Motion.Mobile.Core.BLE;
using Motion.Mobile.Utilities;
using Motion.Core.Data.UserData;
using Motion.Core.Data.AppData;

namespace Motion.Core.SyncHandler
{
	public interface ISyncDeviceHandler
	{

		event EventHandler IncrementProgressBar;
		event EventHandler<SyncDoneEventArgs> SyncDone;
		event EventHandler<EventArgs> SyncStarted;
		event EventHandler<EventArgs> ValidationCodeDisplayed;
		event EventHandler<StatusEventArgs> CodeValidated;


		void SetAdapter(IAdapter adapter);

		void SetDevice(IDevice device);

		void SetWebService(IWebServicesWrapper webservice);

		void SetUserInformation(UserInformation userInfo);

		void SetApplicationInformation(ApplicationInfo appInfo);

		void StartSync(Constants.ScanType scanType);

		ICharacteristic GetServicesCharacteristic(Constants.CharacteristicsUUID uuid);

		void NotifyStateUpdateDone(object sender, CharacteristicReadEventArgs e);

		void ReceiveResponse(object sender, CommandResponseEventArgs e);

		void StartWriteSettings();

		void ProcessCommands();

		void ValidateActivationCode(String enteredCode);

		void CleanUp();

	}
}

