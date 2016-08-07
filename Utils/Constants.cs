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

		//BLE Services
		public enum ServicesUUID
		{
			_FF01, //Trio Custom Service
			_180A, //Device Information
			_180F, //Battery Level
			_1530 //DFU Service
		}

		//BLE Characteristics
		public enum CharacteristicsUUID
		{
			//[Trio Custom Service]
			_FF07, //FF07 Read,Indicate/Notify
			_FF08, //FF08 Read, Indicate/Notify

			//[Device Information]
			_2A29, //Manufacturer
			_2A24, //Model
			_2A25, //Serial
			_2A26, //Firmware Version

			//[Battery Service]
			_2A19, //Battery Level
		}

		//SyncHandler Sequence Names
		public enum SyncHandlerSequence
		{
			EnableFF07,
			EnableFF08,
			ReadSerial,
			ReadModel,
			ReadFwVersion,
			ReadBatteryLevel,
			ReadManufacturer,
			GetWsDeviceInfo
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

