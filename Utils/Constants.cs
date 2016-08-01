using System;
namespace Motion.Core.SyncHandler
{
	public class Constants
	{

		private static volatile Constants instance = new Constants();
		private static object syncRoot = new object();

		public static Constants Instance
		{
			get
			{
				if (instance == null)
				{
					lock (syncRoot)
					{
						if (instance == null)
							instance = new Constants();
					}
				}
				return instance;
			}
		}

		//Streamlines DB Web Services Method Names
		public enum StreamlinesWebServiceMethod
		{
			SendApplicationInfo,
			ValidateUser,
			UploadData,
			UploadSignData,
			UploadSeizureData,
			UploadTallies,
			UploadSurveyResponse,
			UploadDataCompleted,
			UploadCommandResponse,
			UploadStepDataHeader,
			UploadRecordTimeData,
			UploadActivitySummary,
			GetDeviceInfo,
			ActivateDeviceWithMember,
			NotifySettingsUpdate,
			NotifyMessageSettingsUpdate,
			NotifyAlarmSettingsUpdate,
			NotifyFirmwareUpdate,
			GetOnlinePortal,
			GetFirmware,
			GenerateSerial,
			UnpairMemberDevice,
			RegisterTestDevice,
			GetMesagges,
			GetActivitySummary,
		}


		//Derm DB Web Services Method Names
		public enum DermWebServiceMethod
		{
			ENCRYPT_CREDENTIALS,
			SINGLE_SIGN_ON,
			VALIDATE_USER,

		}

	}
}

