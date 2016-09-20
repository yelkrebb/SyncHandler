using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Motion.Core.Data.BleData;
using Motion.Core.Data.BleData.Trio;
using Motion.Core.Data.BleData.Trio.Others;
using Motion.Core.Data.BleData.Trio.SettingsData;
using Motion.Core.Data.BleData.Trio.StepsData;
using Motion.Core.Data.BleData.Trio.AccelData;
using Motion.Core.Data.WebServiceData.DeviceWebServices;
using Motion.Core.WSHandler;
using Motion.Mobile.Core.BLE;
using Motion.Mobile.Utilities;
using Motion.Core.Data.UserData;
using Motion.Core.Data.WebServiceData;
using Motion.Core.Data.AppData;
using Motion.Core.Data.DeviceData;
using Motion.Core.Data.FirmwareData;
using Motion.Core.Data.MemberData;
using Motion.Core.Data.AccelDataInfo;



namespace Motion.Core.SyncHandler
{
	public class SyncDeviceHandler961 : ISyncDeviceHandler
	{
		public UserInformation UserInformationInstance { get; set; }

		private static readonly object _synclock = new object();
		private static SyncDeviceHandler961 Instance;


		public event EventHandler IncrementProgressBar = delegate { };
		public event EventHandler<SyncDoneEventArgs> SyncDone = delegate { };

		Timer timerForDevice,timerForServer;
		TimerCallback timerCallBack;


		private IAdapter Adapter;
		private IDevice Device;
		private IWebServicesWrapper WebService;

		private ICharacteristic Ff07Char;
		private ICharacteristic Ff08Char;
		private ICharacteristic ReadChar;
		private WebServiceRequestStatus status;

		private TrioDeviceInformation TrioDeviceInformationInstance;
		private DeviceData DeviceDataInstance;


		private UserSettings UserSettingsInstance;
		//private Tallies TalliesInstance;
		private DeviceStatus DeviceStatusInstance;
		private StepsTableData StepsTableDataInstance;
		private ClearEEPROM ClearEEPROMInstance;
		private CompanySettings CompanySettingsInstance;
		private DeviceSettings DeviceSettingsInstance;
		private ExerciseSettings ExerciseSettingsInstance;
		private SeizureSettings SeizureSettingsInstance;
		private SensitivitySettings SensitivitySettingsInstance;
		private DeviceMode DeviceModeInstance;
		private SignatureSettings SignatureSettingsInstance;
		private DisplayOnScreenData DisplayOnScreenInstance;
		private FirmwareDisplaySequenceData FirmwareDisplaySequenceInstance;
		private TalliesData TalliesDataInstance;
		private SeizureBlocksData SeizureBlocksDataInstance;
		private StepsData StepsDataInstance;
		private SignatureData SignatureDataInstance;
		private SeizureData SeizureDataInstance;




		private ActivateDeviceWithMember WSActivateDeviceWithMemberInstance;
		private NotifySettingsUpdate WSNotifySettingsUpdateInstance;
		private GetDeviceInformation WSGetDeviceInformationInstance;
		private UploadTalliesData UploadTalliesDataInstance;
		private UploadStepsData WSUploadStepsDataInstance;
		private UploadSignatureData WSUploadSignatureDataInstance;
		private UploadSeizureData WSUploadSeiureDataInstance;
		private UnpairMemberDevice WSUnpairMemberDeviceInstance;
		private UploadCommandResponse WSUploadCommandResponseInstance;

		private ApplicationUpdateSettings AppUpdateSettingsInstance;
		private FirmwareUpdateInfo FirmwareUpdateInfoInstance;
		private MemberServerProfile MemberServerProfileInstance;



		private Queue<Constants.SyncHandlerSequence> ProcessQeueue = new Queue<Constants.SyncHandlerSequence>();
		private Constants.SyncHandlerSequence Command;
		private EventHandler<CharacteristicReadEventArgs> NotifyStateUpdated = null;
		public event EventHandler<StatusEventArgs> FourDigitCodeChecked = delegate { };

		private Constants.ScanType ScanType;
		private BLEParsingStatus ParsingStatus;
		private List<int> ScreenFlowList;
		private List<DateTime> SignatureUploadedDatesList;
		private List<StepsTableParameters> SignatureToBeUploadedTableList;
		private List<SeizureBlockInfo> SeizureToBeUploadedList;
		private List<StepsTableParameters> TimeChangeTableDataList;
		private SeizureBlockInfo SeizureBlockInfoInstance;
		private StepsTableParameters StepTableParamInstance;
		private int updateType;
		private bool clearTracker;
		private int timeDiff;
		private DateTime currentDateForStepTable;
		private int currentStartTimeForStepTable;
		private int currentEndTimeForStepTable;
	/*	private bool isUploadingSignData;
		private bool isUploadingSeizureData;
		private bool isUploadingStepsData;
		private bool isGettingDeviceInformation;*/
		private bool isReadingCurrentHour;
		private int seizureDataReadTimeDuration;
		private int signDataReadTimeDuration;
		private int stepDataReadTimeDuration;
		private int ServerReadTimeDuration;
		private int DeviceReadTimeDuration;
		private bool isUnFlaggingTableHeaderDueToTimeChange;
		private bool clearMessagesOnly;
		private bool isResettingTrio;



		private byte[] CommandRequest;
		private byte[] ActivationCode;

		//flag for progress increment eligibility
		//value will only be true if device configuration is done 
		//as well as the reading of necessary characteristic values are also done.
		private Boolean StartIncrementProgress;

		private List<byte[]> PacketsReceived = new List<byte[]>();

		private SyncDeviceHandler961()
		{
			this.TimeChangeTableDataList = new List<StepsTableParameters>();
		}

		public static SyncDeviceHandler961 GetInstance()
		{
			if (Instance == null)
			{
				lock (_synclock)
				{
					if (Instance == null)
					{
						Instance = new SyncDeviceHandler961();

					}
				}
			}

			return Instance;
		}

		public void CleanUp()
		{
			this.ProcessQeueue.Clear();
			this.Command = Constants.SyncHandlerSequence.Default;
			this.CommandRequest = new byte[]{ };
			this.PacketsReceived.Clear();
			this.Device = null;
			this.StartIncrementProgress = false;

			if (this.timerForDevice != null)
			{
				this.timerForDevice.Cancel();
				this.timerForDevice = null;
			}

			if (this.timerForServer != null)
			{
				this.timerForServer.Cancel();
				this.timerForServer = null;
			}
		}

		public void StartSync(Constants.ScanType scanType)
		{
			this.ScanType = scanType;
			this.ProcessQeueue.Clear();
			this.StartIncrementProgress = false;
			this.isReadingCurrentHour = false;
			this.isUnFlaggingTableHeaderDueToTimeChange = false;
			this.clearMessagesOnly = false;

			TrioDeviceInformationInstance = new TrioDeviceInformation();

			if (this.ScanType == Constants.ScanType.ACTIVATION)
			{
				Debug.WriteLine("SyncDeviceHandler961: Start activation...");
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.EnableFF07);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.EnableFF08);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadModel);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSerial);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadFwVersion);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadBatteryLevel);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadManufacturer);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteScreenDisplay);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsActivateDeviceWithMember);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceSettings);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ClearEEProm);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteUserSettings);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteExerciseSettings);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteCompanySettings);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteSignatureSettings);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteSeizureSettings);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteScreenFlow);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceSensitivity);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceStatus);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceMode);



			}
			else if (this.ScanType == Constants.ScanType.SYNCING)
			{
				
				Debug.WriteLine("SyncDeviceHandler961: Start syncing...");
				//this.GetServicesCharacterisitic();
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.EnableFF07);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.EnableFF08);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadModel);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSerial);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadFwVersion);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadBatteryLevel);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadManufacturer);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadDeviceSettings);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadDeviceStatus);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsGetDeviceInfo);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadTallies);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUploadTallies);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSeizureSettings);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSeizureBlocks);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadStepsHeader);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadUserSettings);


				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadTallies);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadDeviceInformation);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadUserSettings);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadDeviceStatus);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadDeviceSettings);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadStepsHeader);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadCurrentHour);
			}
			this.ProcessCommands();
		}

		public async void ProcessCommands()
		{
			Debug.WriteLine("SyncHandler: ProcessCommands");

			if (this.ProcessQeueue.Count > 0)
			{
				Command = this.ProcessQeueue.Dequeue();

			}
			else {
				Debug.WriteLine("No more commands to be processed! Disconnecting device.");
				this.Adapter.DisconnectDevice(this.Device);
				return;
			}

			ICharacteristic chr = null;

			switch (Command)
			{
				case Constants.SyncHandlerSequence.EnableFF07:
					Debug.WriteLine("SyncDeviceHandler961: Enabling FF07 characteristic");
					Ff07Char = GetServicesCharacteristic(Constants.CharacteristicsUUID._FF07);
					if (Ff07Char == null)
					{
						Debug.WriteLine("FF07 is NULL. Disconnecting device.");
						this.Adapter.DisconnectDevice(Device);
					}
					else {
						Ff07Char.StartUpdates();
					}
					break;
				case Constants.SyncHandlerSequence.EnableFF08:
					Debug.WriteLine("SyncDeviceHandler961: Enabling FF08 characteristic");
					Ff08Char = GetServicesCharacteristic(Constants.CharacteristicsUUID._FF08);
					if (Ff08Char == null)
					{
						Debug.WriteLine("FF08 is NULL. Disconnecting device.");
						this.Adapter.DisconnectDevice(Device);
					}
					else {
						Ff08Char.StartUpdates();
					}
					break;
				case Constants.SyncHandlerSequence.ReadModel:
					Debug.WriteLine("SyncDeviceHandler961: Reading model from characteristic.");
					ReadChar = GetServicesCharacteristic(Constants.CharacteristicsUUID._2A24);
					chr = await ReadChar.ReadAsync();
					int num = 0;
					int.TryParse(System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length).Replace("PE", "").Replace("FT", ""), out num);
					Debug.WriteLine("Model: " + num);
					this.TrioDeviceInformationInstance.ModelNumber = num;
					string[] deviceNameComponents = this.Device.Name.Split(new Char[] {'-'});
					if ( deviceNameComponents.Count() == 3)
						this.TrioDeviceInformationInstance.BroadcastType = deviceNameComponents[1];
					else
						this.TrioDeviceInformationInstance.BroadcastType = "";

					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadSerial:
					Debug.WriteLine("SyncDeviceHandler961: Reading serial from characterisitic.");
					ReadChar = GetServicesCharacteristic(Constants.CharacteristicsUUID._2A25);
					chr = await ReadChar.ReadAsync();
					long serial = 0;
					long.TryParse(System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length), out serial);
					Debug.WriteLine("Serial: " + serial);
					this.TrioDeviceInformationInstance.SerialNumber = serial;
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadFwVersion:
					Debug.WriteLine("SyncDeviceHandler961: Reading fw version from characteristic.");
					ReadChar = GetServicesCharacteristic(Constants.CharacteristicsUUID._2A26);
					chr = await ReadChar.ReadAsync();
					float fwVer = 0;
					float.TryParse(System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length), out fwVer);
					Debug.WriteLine("Firmware Version: " + fwVer);
					this.TrioDeviceInformationInstance.FirmwareVersion = fwVer;
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadManufacturer:
					Debug.WriteLine("SyncDeviceHandler961: Reading manufacturer from characteristic.");
					ReadChar = GetServicesCharacteristic(Constants.CharacteristicsUUID._2A29);
					chr = await ReadChar.ReadAsync();
					Debug.WriteLine("Manufacturer: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					this.InitDataModelObjects();
					this.Adapter.CommandResponse += ReceiveResponse;
					//this.isGettingDeviceInformation = false;
					//if (this.ScanType == Constants.ScanType.SYNCING)
					//	this.isGettingDeviceInformation = true;
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadBatteryLevel:
					Debug.WriteLine("SyncDeviceHandler961: Reading battery level from characteristic.");
					ReadChar = GetServicesCharacteristic(Constants.CharacteristicsUUID._2A19);
					chr = await ReadChar.ReadAsync();
					int battery = (int) chr.Value[0];
					Debug.WriteLine("Battery Level: " + battery);
					this.TrioDeviceInformationInstance.BatteryLevel = battery;
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadTallies:
					Debug.WriteLine("SyncDeviceHandler961: Read Device Tallies");
					CommandRequest = new byte[] { 0x1B, 0x5D };
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadDeviceInformation:
					Debug.WriteLine("SyncDeviceHandler961: Read Device Information");
					CommandRequest = new byte[] { 0x1B, 0x40 };
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadDeviceStatus:
					Debug.WriteLine("SyncDeviceHandler961: Read Device Status");
					CommandRequest = await DeviceStatusInstance.GetReadCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadDeviceSettings:
					Debug.WriteLine("SyncDeviceHandler961: Read Device Settings");
					CommandRequest = await DeviceSettingsInstance.GetReadCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadSeizureSettings:
					Debug.WriteLine("SyncDeviceHandler961: Read Seizure Settings");
					CommandRequest = await SeizureSettingsInstance.GetReadCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;	
				case Constants.SyncHandlerSequence.ReadUserSettings:
					Debug.WriteLine("SyncDeviceHandler961: Read User Settings");
					CommandRequest = await UserSettingsInstance.GetReadCommand();
					Debug.WriteLine("Read User Settings: " + Motion.Mobile.Utilities.Utils.ByteArrayToHexString(CommandRequest));
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadStepsHeader:
					Debug.WriteLine("SyncDeviceHandler961: Read Steps Header");
					CommandRequest = await StepsTableDataInstance.GetReadStepTableDataCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadSeizureBlocks:
					Debug.WriteLine("SyncDeviceHandler961: Read Seizure Table");
					CommandRequest = await SeizureBlocksDataInstance.GetReadSeizureBlocksDataCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadHourlySteps:
					Debug.WriteLine("SyncDeviceHandler961: Read Hourly Steps");
					DateTime dt = this.currentDateForStepTable;
					CommandRequest = await StepsDataInstance.getReadHourRangeCommand(dt, this.currentStartTimeForStepTable, currentEndTimeForStepTable);
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					Debug.WriteLine("Read Hour Range Settings: " + Motion.Mobile.Utilities.Utils.ByteArrayToHexString(CommandRequest));
					break;
				case Constants.SyncHandlerSequence.ReadCurrentHour:
					Debug.WriteLine("SyncDeviceHandler961: Read Current Hour");
					CommandRequest = await StepsDataInstance.getReadCurrentHourCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadSignature:
					Debug.WriteLine("SyncDeviceHandler961: Read Signature Data");
					StepsTableParameters sigData = this.SignatureToBeUploadedTableList.ElementAt(0);
					SignatureDataInstance.SDYear = sigData.dbYear;
					SignatureDataInstance.SDMonth = sigData.dbMonth;
					SignatureDataInstance.SDDay = sigData.dbDay;
					CommandRequest = await SignatureDataInstance.GetReadSignDataCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);

					// insert timer here
					break;
				case Constants.SyncHandlerSequence.ReadSeizure:
					Debug.WriteLine("SyncDeviceHandler961: Read seizure Data");
					SeizureBlockInfo seizureBlock = this.SeizureToBeUploadedList.ElementAt(0);
					SeizureDataInstance.SeizureRecToRead = seizureBlock.sbseizureRecordNo;
					SeizureDataInstance.BlockNumber = seizureBlock.sbSeizureBlock;
					CommandRequest = await SeizureDataInstance.GetReadCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);

					// insert timer here
					break;
				case Constants.SyncHandlerSequence.WriteStepsHeader:
					Debug.WriteLine("SyncDeviceHandler961: Writing steps header");
					CommandRequest = await StepsTableDataInstance.GetWriteStepTableDataCommand(StepTableParamInstance);
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.WriteDeviceSettings:
					Debug.WriteLine("SyncDeviceHandler961: Writing device settings");
					CommandRequest = await DeviceSettingsInstance.GetWriteCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.WriteUserSettings:
					Debug.WriteLine("SyncDeviceHandler961: Writing user settings");
					CommandRequest = await UserSettingsInstance.GetWriteCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.WriteExerciseSettings:
					Debug.WriteLine("SyncDeviceHandler961: Writing exercise settings");
					CommandRequest = await ExerciseSettingsInstance.GetWriteCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.WriteCompanySettings:
					Debug.WriteLine("SyncDeviceHandler961: Writing company settings");
					CommandRequest = await CompanySettingsInstance.GetWriteCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.WriteSignatureSettings:
					Debug.WriteLine("SyncDeviceHandler961: Writing signature settings");
					CommandRequest = await SignatureSettingsInstance.GetWriteCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.WriteSeizureSettings:
					Debug.WriteLine("SyncDeviceHandler961: Writing seizure settings");
					CommandRequest = await SeizureSettingsInstance.GetWriteCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
		          	break;
				case Constants.SyncHandlerSequence.WriteScreenFlow:
					Debug.WriteLine("SyncDeviceHandler961: Writing screenflow settings");
					CommandRequest = await FirmwareDisplaySequenceInstance.GetWriteCommand(this.ScreenFlowList);
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.WriteDeviceSensitivity:
					Debug.WriteLine("SyncDeviceHandler961: Writing device sensitivity settings");
					SensitivitySettingsInstance.SensitivityOld = DeviceDataInstance.TrackerSensitivity;
					CommandRequest = await SensitivitySettingsInstance.GetWriteCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.WriteDeviceStatus:
					Debug.WriteLine("SyncDeviceHandler961: Writing device status settings");
					CommandRequest = await DeviceStatusInstance.GetWriteCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.WriteDeviceMode:
					Debug.WriteLine("SyncDeviceHandler961: Writing device status settings");
					DeviceModeInstance.EnableBroadcastAlways = DeviceDataInstance.AdvertiseMode;
					CommandRequest = await DeviceModeInstance.GetWriteCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;	
				case Constants.SyncHandlerSequence.WriteScreenDisplay:
					Debug.WriteLine("SyncDeviceHandler961: Writing screen display");
					this.ActivationCode = Utils.GetRandomDigits(4); //4 random digits to be generated
					// For testing purposes only
					this.ActivationCode[0] = 0x31;
					this.ActivationCode[1] = 0x32;
					this.ActivationCode[2] = 0x33;
					this.ActivationCode[3] = 0x34;
					// For testing purposes only
					Debug.WriteLine("Random digits: " + Motion.Mobile.Utilities.Utils.ByteArrayToHexString(this.ActivationCode));
					CommandRequest = await DisplayOnScreenInstance.GetWriteCommand(this.ActivationCode);
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ClearEEProm:
					Debug.WriteLine("SyncDeviceHandler961: Clear eeprom");
					ClearEEPROMInstance.ExerciseData = this.clearMessagesOnly?false:true;
					ClearEEPROMInstance.SettingsData = false;
					ClearEEPROMInstance.SurveyQuestions = true;
					ClearEEPROMInstance.Tallies = true;
					ClearEEPROMInstance.MedicineAlarm = true;
					ClearEEPROMInstance.MotivationalQuotes = true;
					CommandRequest = await ClearEEPROMInstance.GetWriteCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;

				case Constants.SyncHandlerSequence.WsActivateDeviceWithMember:
					Debug.WriteLine("SyncDeviceHandler961: WS Activate Device With Member");
					WSActivateDeviceWithMemberInstance.request.TrackerModel = TrioDeviceInformationInstance.ModelNumber;
					WSActivateDeviceWithMemberInstance.request.TrackerSerialNumber = TrioDeviceInformationInstance.SerialNumber;
					WSActivateDeviceWithMemberInstance.request.TrackerBtAdd = "";
					WSActivateDeviceWithMemberInstance.request.ApplicationID = 14487;
					WSActivateDeviceWithMemberInstance.request.MemberDermID = 66525;
					WSActivateDeviceWithMemberInstance.request.TrackerFirmwareVersion = 4.3f;
					this.status = await WSActivateDeviceWithMemberInstance.PostRequest();

					if (this.status == WebServiceRequestStatus.SUCCESS)
					{
						Debug.WriteLine("Success");
						if (WSActivateDeviceWithMemberInstance.response != null)
						{
							this.SetSettingsFromActivateDeviceWithMemberResponseWS(WSActivateDeviceWithMemberInstance.response);
							ProcessCommands();
						}
						else
						{
							Debug.WriteLine("ActivateDeviceWithMemeber Response is " + WSActivateDeviceWithMemberInstance.response.ResponseMessage);
							this.Adapter.DisconnectDevice(this.Device);
						}

					}
					//this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.WsGetDeviceInfo:
					Debug.WriteLine("SyncDeviceHandler961: WS Request GetDeviceInfo");
					WSGetDeviceInformationInstance.request.TrackerModel = TrioDeviceInformationInstance.ModelNumber;
					WSGetDeviceInformationInstance.request.TrackerSerialNumber = TrioDeviceInformationInstance.SerialNumber;
					WSGetDeviceInformationInstance.request.DeviceDateTime = Motion.Mobile.Utilities.Utils.GetDeviceDateTimeWithFormat(DateTime.Now);
					WSGetDeviceInformationInstance.request.ApplicationID = 14487;
					WSGetDeviceInformationInstance.request.DevicePairingStatus = 1;
					WSGetDeviceInformationInstance.request.TrackerFirmwareVersion = 4.3f;
					this.status = await WSGetDeviceInformationInstance.PostRequest();

					this.ServerReadTimeDuration = 0;
					if (this.status == WebServiceRequestStatus.SUCCESS)
					{
						Debug.WriteLine("Success");
						if (WSGetDeviceInformationInstance.response != null)
						{
							this.isResettingTrio = false;
							timerForServer = new Timer(timerCallBack, new object(), 0, 1000);
							timerForServer.Increment += TimerForServer_Increment;
							this.SetSettingsFromGetDeviceInfoResponseWS(WSGetDeviceInformationInstance.response);
							ProcessCommands();
						}
						else
						{
							Debug.WriteLine("WS Request GetDeviceInfo Response is " + WSGetDeviceInformationInstance.response.ResponseMessage);
							this.Adapter.DisconnectDevice(this.Device);
						}

					}
					//else if  Tobe Added?
					else if (string.Equals(WSGetDeviceInformationInstance.response.ResponseStatus, "reset"))
					{
						this.isResettingTrio = true;
						DeviceStatusInstance.PairingStatus = 1;
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceStatus);
						this.ProcessCommands();
						Debug.WriteLine("Device Not Paired...");
					}
					else
					{ 
						Debug.WriteLine("Get Device Info Failed :(");
					}
						


					break;

				case Constants.SyncHandlerSequence.WsUploadTallies:
					Debug.WriteLine("SyncDeviceHandler961: WS Upload Tallies");

					UserInformationInstance = new UserInformation();
					UserInformationInstance.MemberDeviceId = 72277;
					UserInformationInstance.ApplicationID = 14487;
					UserInformationInstance.PlatformID = 1036;


					UploadTalliesDataInstance.request.DeviceModel = TrioDeviceInformationInstance.ModelNumber;
					UploadTalliesDataInstance.request.DeviceSerial = TrioDeviceInformationInstance.SerialNumber;
					UploadTalliesDataInstance.request.MemberDeviceID = UserInformationInstance.MemberDeviceId;
					UploadTalliesDataInstance.request.ApplicationID = UserInformationInstance.ApplicationID;
					UploadTalliesDataInstance.request.PlatformID = UserInformationInstance.PlatformID;
					UploadTalliesDataInstance.request.DeviceDateTime = Motion.Mobile.Utilities.Utils.GetServerDateTimeFromString(MemberServerProfileInstance.ServerDateTime);
					UploadTalliesDataInstance.request.talliesData = TalliesDataInstance;

					this.status = await UploadTalliesDataInstance.PostRequest();

					if (this.status == WebServiceRequestStatus.SUCCESS)
					{
						Debug.WriteLine("Success");
						if ((AppUpdateSettingsInstance.UpdateFlag && AppUpdateSettingsInstance.UpdateType != 2) || !AppUpdateSettingsInstance.UpdateFlag)
						{
							if(!this.clearTracker)
							this.ProcessCommands();
						}

					}
					else
					{
						Debug.WriteLine("Upload Tallies Response is " + UploadTalliesDataInstance.response.ResponseMessage);
						this.Adapter.DisconnectDevice(this.Device);
					}
					break;
				case Constants.SyncHandlerSequence.WsUploadSteps:

					this.CreateUploadStepsDataRequest(WSUploadStepsDataInstance.request);
					this.status = await WSUploadStepsDataInstance.PostRequest();
					if (this.status == WebServiceRequestStatus.SUCCESS)
					{
						Debug.WriteLine("Success");
						if (this.isReadingCurrentHour)
						{
							//READ_DEVICE_SETTING
							this.checkTimeDifferenceAndUpdateDeviceTime();
						}
						else
						{
							//UpdateStepDataTable
							StepTableParamInstance = this.UpdateStepDataTable();
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteStepsHeader);
							this.ProcessCommands();

						}

					}
					else
					{
						Debug.WriteLine("Upload Steps Response is " + WSUploadStepsDataInstance.response.ResponseMessage);
						this.Adapter.DisconnectDevice(this.Device);
					}
					break;
				case Constants.SyncHandlerSequence.WsUploadSignature:
					this.CreateUploadSignatureRequestData(WSUploadSignatureDataInstance.request);
					this.status = await WSUploadSignatureDataInstance.PostRequest();
					if (this.status == WebServiceRequestStatus.SUCCESS)
					{
						Debug.WriteLine("Success");
						if (this.SignatureToBeUploadedTableList.Count() > 0)
						{
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSignature);
							this.ProcessCommands();
						}
						else
						{
							if (this.MemberServerProfileInstance.UpdateFlag > 0)
							{
								this.UpdateSettingsFlag(Utils.USER_SETTINGS_BIT_LOC);
							}
							else
							{
								//if(READ SURVEY)
								//else
								// update settings 
								//CLEAR_MESSAGES_SETTINGS_BIT_LOC
								this.UpdateSettingsFlag(Utils.CLEAR_MESSAGES_SETTINGS_BIT_LOC);
							}
						}
							

					}
					else
					{
						Debug.WriteLine("Upload Signature Response is " + WSUploadSignatureDataInstance.response.ResponseMessage);
						this.Adapter.DisconnectDevice(this.Device);
					}
					break;
				case Constants.SyncHandlerSequence.WsUploadProfile:
					break;
				case Constants.SyncHandlerSequence.WsUnpairDevice:
					WSUnpairMemberDeviceInstance.request.DeviceID = WSActivateDeviceWithMemberInstance.response.DeviceID;
					WSUnpairMemberDeviceInstance.request.MemberID = 56375;
					WSUnpairMemberDeviceInstance.request.MemberDeviceID = 72273;
					this.status = await WSUploadSeiureDataInstance.PostRequest();
					if (this.status == WebServiceRequestStatus.SUCCESS)
					{
						Debug.WriteLine("Deactivating Device Successful");
					}
					break;
				case Constants.SyncHandlerSequence.WsUploadSeizure:
					this.CreateUploadSeizureRequestData(WSUploadSeiureDataInstance.request);
					this.status = await WSUploadSeiureDataInstance.PostRequest();
					if (this.status == WebServiceRequestStatus.SUCCESS)
					{
						Debug.WriteLine("Success");
						if (this.SeizureToBeUploadedList.Count() > 0)
						{
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSeizure);
							this.ProcessCommands();
						}
						else
						{
							if (this.SignatureToBeUploadedTableList.Count() > 0)
							{
								this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSignature);
								this.ProcessCommands();
							}
							else
							{
								if (this.MemberServerProfileInstance.UpdateFlag > 0)
								{
									this.UpdateSettingsFlag(Utils.USER_SETTINGS_BIT_LOC);
								}
								else
								{
									//if(READ SURVEY)
									//else
									// update settings 
									//CLEAR_MESSAGES_SETTINGS_BIT_LOC
									this.UpdateSettingsFlag(Utils.CLEAR_MESSAGES_SETTINGS_BIT_LOC);
								}
							}
						}


					}
					else
					{
						Debug.WriteLine("Upload Seizure Response is " + WSUploadSeiureDataInstance.response.ResponseMessage);
						this.Adapter.DisconnectDevice(this.Device);
					}
					break;
				case Constants.SyncHandlerSequence.WsSendNotifySettingsUpdate:
					Debug.WriteLine("SyncDeviceHandler961: WS Notify Settings Update");

					DateTime currentDateTime = DateTime.Now;

					long dateTimeInUnix = (long)Motion.Mobile.Utilities.Utils.DateTimeToUnixTimestamp(currentDateTime);

					WSNotifySettingsUpdateInstance.request.MemberID = 56375;
					WSNotifySettingsUpdateInstance.request.MemberDeviceID = 72273;
					WSNotifySettingsUpdateInstance.request.LastSyncSettingDateTime = dateTimeInUnix;
					WSNotifySettingsUpdateInstance.request.TrackerModel = TrioDeviceInformationInstance.ModelNumber;
					WSNotifySettingsUpdateInstance.request.UpdateFlag = 3327;
					WSNotifySettingsUpdateInstance.request.SettingType = 3327;
					WSNotifySettingsUpdateInstance.request.DevicePairingStatus = 0;
					WSNotifySettingsUpdateInstance.request.DeviceDateTime = null;
					WSNotifySettingsUpdateInstance.request.BatteryLevel = TrioDeviceInformationInstance.BatteryLevel;


					this.status = await WSNotifySettingsUpdateInstance.PostRequest();
					if (this.status == WebServiceRequestStatus.SUCCESS)
					{
						Debug.WriteLine("WS Notify Settings Update Successful");
					}
					break;
				case Constants.SyncHandlerSequence.WsUploadCommandResponse:
					Debug.WriteLine("SyncDeviceHandler961: WS Upload Command Response");
					WSUploadCommandResponseInstance.request.commandRespData.UploadCommand = 0x58;
					WSUploadCommandResponseInstance.request.DeviceModel = TrioDeviceInformationInstance.ModelNumber;
					WSUploadCommandResponseInstance.request.DeviceSerial = TrioDeviceInformationInstance.SerialNumber;
					WSUploadCommandResponseInstance.request.MemberDeviceID = 72277;
					WSUploadCommandResponseInstance.request.ApplicationID = 14487;
					WSUploadCommandResponseInstance.request.PlatformID = 1036;
					WSUploadCommandResponseInstance.request.SynchronizationID = this.MemberServerProfileInstance.SynchronizationID;
					if (this.status == WebServiceRequestStatus.SUCCESS)
					{
						Debug.WriteLine("WS Upload Command Response");
					}
					break;	
				default:
					Debug.WriteLine("SyncDeviceHandler961: Unable to identify command.");
					break;
			}

		}

		public void SetAdapter(IAdapter adapter)
		{
			this.Adapter = adapter;
		}

		public void SetDevice(IDevice device)
		{
			this.Device = device;

		}

		public ICharacteristic GetServicesCharacteristic(Constants.CharacteristicsUUID uuid)
		{
			ICharacteristic characterisitic = null;

			foreach (var service in this.Device.Services)
			{
				foreach (var chr in service.Characteristics)
				{
					if (chr.Uuid.ToString().ToUpper().Contains(uuid.ToString().Replace("_", "")))
					{
						Debug.WriteLine("Characteristic Found: " + chr.ID);
						characterisitic = chr;
						if (characterisitic.CanUpdate)
						{
							if (NotifyStateUpdated == null)
							{
								Debug.WriteLine("Subscribing notifystateupdate!");
								NotifyStateUpdated = new EventHandler<CharacteristicReadEventArgs>(NotifyStateUpdateDone);
								characterisitic.NotificationStateValueUpdated += NotifyStateUpdateDone;
															}
						}
						break;
					}
				}
			}

			return characterisitic;
		}


		public void NotifyStateUpdateDone(object sender, CharacteristicReadEventArgs e)
		{
			Debug.WriteLine("SyncHandlerDevice961: Notification State Updated: " + e.Characteristic.ID);
			switch (Command)
			{
				case Constants.SyncHandlerSequence.EnableFF07:
					Debug.WriteLine("Done enabling FF07");
					break;
				case Constants.SyncHandlerSequence.EnableFF08:
					Debug.WriteLine("Done enabling FF08");
					break;
				default:
					Debug.WriteLine("Unknown response!");
					break;

			}
			this.ProcessCommands();
		}

		public async void ReceiveResponse(object sender, CommandResponseEventArgs e)
		{
			Debug.WriteLine("Receiving Response: " + Motion.Mobile.Utilities.Utils.ByteArrayToHexString(e.Data));

			if (this.StartIncrementProgress)
			{
				this.IncrementProgressBar(this, new EventArgs() { });
			}
			switch (this.Command)
			{
				case Constants.SyncHandlerSequence.WriteScreenDisplay:
					this.ParsingStatus = await DisplayOnScreenInstance.ParseData(e.Data);
					// For testing purposes only
					bool success = ValidateActivationCode("1234");
					if (success)
					{
						StatusEventArgs args = new StatusEventArgs();
						args.isEqual = success;
						this.FourDigitCodeChecked(this,args);
						this.ProcessCommands();
					}
						
					break;
				case Constants.SyncHandlerSequence.ReadTallies:
					Debug.WriteLine("Receiving device tallies");
					this.PacketsReceived.Add(e.Data);
					if (Utils.LastPacketReceived(2, e.Data))
					{
						Debug.WriteLine("Last packet received...");
						//Process Data Here...
						this.ParsingStatus = await TalliesDataInstance.ParseData(this.PacketsReceived.SelectMany(a => a).ToArray());
						if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
						{
							this.PacketsReceived.Clear();
							this.ProcessCommands();
						}
						else {
							this.Adapter.DisconnectDevice(this.Device);
						}

					}
					break;
				case Constants.SyncHandlerSequence.ReadDeviceInformation:
					Debug.WriteLine("Receving device information");
					this.ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadDeviceStatus:
					Debug.WriteLine("Receving device status");

					this.ParsingStatus = await DeviceStatusInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.ProcessCommands();
					}
					else {
						this.Adapter.DisconnectDevice(this.Device);
					}
					break;
				case Constants.SyncHandlerSequence.ReadDeviceSettings:
					Debug.WriteLine("Receiving device settings");
					this.ParsingStatus = await DeviceSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						if (this.ScanType == Constants.ScanType.ACTIVATION)
						{
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsSendNotifySettingsUpdate);

						}
						else
						{ 
							timerForDevice = new Timer(timerCallBack, new object(), 0, 1000);
							timerForDevice.Increment += TimerForDevice_Increment;
						}
						this.ProcessCommands();
					}
					else {
						this.Adapter.DisconnectDevice(this.Device);
					}
					break;
				case Constants.SyncHandlerSequence.ReadSeizureSettings:
					Debug.WriteLine("Receiving seizure settings");
					this.ParsingStatus = await SeizureSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.ProcessCommands();
					}
					else {
						this.Adapter.DisconnectDevice(this.Device);
					}
					break;	
				case Constants.SyncHandlerSequence.ReadUserSettings:
					Debug.WriteLine("Receiving user settings: ");
					this.ParsingStatus = await UserSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						Debug.WriteLine("User RMR: " + UserSettingsInstance.RestMetabolicRate);
						this.ReadStepsData();
					}
					else {
						this.Adapter.DisconnectDevice(this.Device);
					}
					break;
				case Constants.SyncHandlerSequence.ReadStepsHeader:
					Debug.WriteLine("Receiving steps header data");
					this.PacketsReceived.Add(e.Data);
					if (Utils.LastPacketReceived(2, e.Data))
					{
						Debug.WriteLine("Last packet received...");
						byte[] data = new byte[20 * this.PacketsReceived.Count];
						int len = 0;
						int currentIndex = 0;
						for (int i = 0; i < this.PacketsReceived.Count; i++)
						{
							len = this.PacketsReceived.ElementAt(i).ToArray().Length;
							Array.Copy(this.PacketsReceived.ElementAt(i).ToArray(), Constants.INDEX_ZERO, data, currentIndex, len);
							Debug.WriteLine("Loop Response: " + Motion.Mobile.Utilities.Utils.ByteArrayToHexString(data));
							currentIndex += len;

						}
						this.ParsingStatus = await StepsTableDataInstance.ParseData(data);

						if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
						{
							foreach (StepsTableParameters stepTable in StepsTableDataInstance.stepsDataTable)
							{
								Debug.WriteLine("Date: " + stepTable.tableDate);

							}




							this.PacketsReceived.Clear();

							this.ProcessCommands();
						}
						else {
							this.Adapter.DisconnectDevice(this.Device);
						}
					}
					break;
				case Constants.SyncHandlerSequence.ReadSeizureBlocks:
					Debug.WriteLine("Receiving seizure table data");
					this.ParsingStatus = await SeizureBlocksDataInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.ProcessCommands();
					}
					else {
						this.Adapter.DisconnectDevice(this.Device);
					};
					break;
				case Constants.SyncHandlerSequence.ReadHourlySteps:
					Debug.WriteLine("Receiving hourly steps data");
					this.PacketsReceived.Add(e.Data);
					if (Utils.TerminatorFound(0xFF, 4, e.Data))
					{
						Debug.WriteLine("Last packet received...");
						this.ParsingStatus = await StepsDataInstance.ParseData(this.PacketsReceived.SelectMany(a => a).ToArray());
						if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
						{
							this.PacketsReceived.Clear();
							//this.isUploadingStepsData = true;
							//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadDeviceSettings);
							if (StepsDataInstance.dailyData != null && StepsDataInstance.dailyData.Count > 0 && StepsDataInstance.dailyData[0].stepsYear != 0)
								this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUploadSteps);
							this.ProcessCommands();
						}
						else {
							Debug.WriteLine("Error Parsing: " +  this.ParsingStatus);
							this.Adapter.DisconnectDevice(this.Device);
						};
					}
					break;
				case Constants.SyncHandlerSequence.ReadCurrentHour:
					Debug.WriteLine("Receiving current hour steps data");
					this.PacketsReceived.Add(e.Data);
					if (Utils.TerminatorFound(0xFF, 4, e.Data))
					{
						Debug.WriteLine("Last packet received...");
						this.ParsingStatus = await StepsDataInstance.ParseData(this.PacketsReceived.SelectMany(a => a).ToArray());
						if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
						{
							this.PacketsReceived.Clear();
							if (StepsDataInstance.dailyData != null && StepsDataInstance.dailyData.Count > 0 && StepsDataInstance.dailyData[0].stepsYear != 0)
								this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUploadSteps);
							this.ProcessCommands();
						}
						else {
							this.Adapter.DisconnectDevice(this.Device);
						};
					}
					break;
				case Constants.SyncHandlerSequence.ReadSignature:
					Debug.WriteLine("Receiving signature data");
					this.PacketsReceived.Add(e.Data);
					if (Utils.TerminatorFound(0xFF, 4, e.Data))
					{
						Debug.WriteLine("Terminator Found...");
						this.ParsingStatus = await SignatureDataInstance.ParseData(this.PacketsReceived.SelectMany(a => a).ToArray());
						if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
						{
							this.SignatureToBeUploadedTableList.RemoveAt(0);
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUploadSignature);
							this.ProcessCommands();
						}

					}
					break;
				case Constants.SyncHandlerSequence.ReadSeizure:
					this.PacketsReceived.Add(e.Data);
					if (Utils.TerminatorFound(0xFF, 4, e.Data))
					{
						Debug.WriteLine("Terminator Found...");
						Debug.WriteLine("Terminator Found...");
						this.ParsingStatus = await SeizureDataInstance.ParseData(this.PacketsReceived.SelectMany(a => a).ToArray());
						if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
						{
							this.SeizureToBeUploadedList.RemoveAt(0);
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUploadSeizure);
							this.ProcessCommands();
						}

					}
					break;
				case Constants.SyncHandlerSequence.WriteStepsHeader:
					Debug.WriteLine("Receiving Write Steps Header response.");
					this.ParsingStatus = await StepsTableDataInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.ProcessWriteTableHeaderResponse();
					}
					else {
						this.Adapter.DisconnectDevice(this.Device);
					};
					break;
				case Constants.SyncHandlerSequence.WriteDeviceSettings:
					Debug.WriteLine("Receiving Write Device Settings response.");
					this.ParsingStatus = await DeviceSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.MemberServerProfileInstance.UpdateFlagChanges = this.MemberServerProfileInstance.UpdateFlagChanges | Utils.DEVICE_SETTINGS;

					}
					else
					{ 
						Debug.WriteLine("Write Device Settings Failed.");
						if (this.ScanType == Constants.ScanType.ACTIVATION)
						{ 
							//unpair
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUnpairDevice);
							this.ProcessCommands();
						}
							
					}

					if (this.ScanType == Constants.ScanType.ACTIVATION)
					{
						if (this.clearTracker)
							this.ProcessCommands();
						else
							this.UpdateSettingsFlag(Utils.USER_SETTINGS_BIT_LOC);
					}
					else
					{
						if (this.clearTracker)
						{
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ClearEEProm);
						}
						if (this.isUnFlaggingTableHeaderDueToTimeChange && this.TimeChangeTableDataList.Count() > 1)
						{
							if (this.UpdateStepDataTable() == null)
							{
								this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteStepsHeader);
								this.ProcessCommands();
							}
						}
						else
						{
							this.CheckSeizureOrSignatureForUpload();
						}

					}
					break;
				case Constants.SyncHandlerSequence.WriteUserSettings:
					Debug.WriteLine("Receiving Write User Settings response.");
					this.ParsingStatus = await UserSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.MemberServerProfileInstance.UpdateFlagChanges = (this.MemberServerProfileInstance.UpdateFlagChanges | Utils.USER_SETTINGS);

					}
					else
					{ 
						//unpair if activating
					}
					this.UpdateSettingsFlag(Utils.EXERCISE_SETTINGS_BIT_LOC);
					break;
				case Constants.SyncHandlerSequence.WriteExerciseSettings:
					Debug.WriteLine("Receiving Write Exercise Settings response.");
					this.ParsingStatus = await ExerciseSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.MemberServerProfileInstance.UpdateFlagChanges = (this.MemberServerProfileInstance.UpdateFlagChanges | Utils.EXERCISE_SETTINGS);
					}
					else
					{ 
						if (this.ScanType == Constants.ScanType.ACTIVATION)
						{
							//unpair
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUnpairDevice);
							this.ProcessCommands();
						}
					}
					this.UpdateSettingsFlag(Utils.COMPANY_SETTINGS_BIT_LOC);
					break;
				case Constants.SyncHandlerSequence.WriteCompanySettings:
					Debug.WriteLine("Receiving Write Company Settings response.");
					this.ParsingStatus = await CompanySettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.MemberServerProfileInstance.UpdateFlagChanges = (this.MemberServerProfileInstance.UpdateFlagChanges | Utils.COMPANY_SETTINGS);

					}
					else
					{ 
						if (this.ScanType == Constants.ScanType.ACTIVATION)
						{
							//unpair
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUnpairDevice);
							this.ProcessCommands();
						}
					}
					this.UpdateSettingsFlag(Utils.SIGNATURE_SETTINGS_BIT_LOC);
					break;
				case Constants.SyncHandlerSequence.WriteSignatureSettings:
					Debug.WriteLine("Receiving Write Signature Settings response.");
					this.ParsingStatus = await SignatureSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.MemberServerProfileInstance.UpdateFlagChanges = (this.MemberServerProfileInstance.UpdateFlagChanges | Utils.SIGNATURE_SETTINGS);
					}
					else
					{
						if (this.ScanType == Constants.ScanType.ACTIVATION)
						{
							//unpair
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUnpairDevice);
							this.ProcessCommands();
						}
					}
					this.UpdateSettingsFlag(Utils.SEIZURE_SETTINGS_BIT_LOC);
					break;
				case Constants.SyncHandlerSequence.WriteSeizureSettings:
					Debug.WriteLine("Receiving Write Seizure Settings response.");
					this.ParsingStatus = await SeizureSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.MemberServerProfileInstance.UpdateFlagChanges = (this.MemberServerProfileInstance.UpdateFlagChanges | Utils.SEIZURE_SETTINGS);
					}
					else
					{
						if (this.ScanType == Constants.ScanType.ACTIVATION)
						{
							//unpair
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUnpairDevice);
							this.ProcessCommands();
						}
					}
					this.UpdateSettingsFlag(Utils.SCREEN_SETTINGS_BIT_LOC);
					break;
				case Constants.SyncHandlerSequence.WriteScreenFlow:
					this.ParsingStatus = await FirmwareDisplaySequenceInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.MemberServerProfileInstance.UpdateFlagChanges = (this.MemberServerProfileInstance.UpdateFlagChanges | Utils.SCREEN_SETTINGS);
					}
					else
					{
						if (this.ScanType == Constants.ScanType.ACTIVATION)
						{
							//unpair
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUnpairDevice);
							this.ProcessCommands();
						}
					}
					this.UpdateSettingsFlag(Utils.SENSITIVITY_SETTINGS_BIT_LOC);
					break;
				case Constants.SyncHandlerSequence.WriteDeviceSensitivity:
					int flag = await SensitivitySettingsInstance.ParseData(e.Data);
					if (flag == 0)
					{
						this.MemberServerProfileInstance.UpdateFlagChanges = (this.MemberServerProfileInstance.UpdateFlagChanges | Utils.SENSITIVITY_SETTINGS);

						if (this.ScanType == Constants.ScanType.SYNCING)
						{
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsSendNotifySettingsUpdate);
							this.ProcessCommands();
						}
						else
						{ 
							DeviceStatusInstance.PairingStatus = 1;
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceStatus);
							this.ProcessCommands();
						}
					}
					else
					{
						if (this.ScanType == Constants.ScanType.ACTIVATION)
						{
							//unpair
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUnpairDevice);
							this.ProcessCommands();
						}
						else
						{
							WSUploadCommandResponseInstance.request.commandRespData.UploadResponse = flag;
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsUploadCommandResponse);
							this.ProcessCommands();
						}
					}
					break;
				case Constants.SyncHandlerSequence.WriteDeviceStatus:
					this.ParsingStatus = await DeviceStatusInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						DeviceStatusInstance.PairingStatus = 0;
						this.MemberServerProfileInstance.UpdateFlagChanges = (this.MemberServerProfileInstance.UpdateFlagChanges | Utils.PAIRING_SETTINGS);
						if (this.ScanType == Constants.ScanType.ACTIVATION)
						{
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceMode);
							this.ProcessCommands();
						}

					}
					else
					{
						Debug.WriteLine("Failed to Set Pairing..");
					}

					if (this.ScanType == Constants.ScanType.SYNCING)
					{
						this.Adapter.DisconnectDevice(this.Device);
					}
					break;

				case Constants.SyncHandlerSequence.WriteDeviceMode:
					this.ParsingStatus = await DeviceStatusInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadDeviceSettings);
						this.ProcessCommands();
					}
					break;
				case Constants.SyncHandlerSequence.ClearEEProm:
					Debug.WriteLine("Receiving clear eeprom: ");
					this.ParsingStatus = await ClearEEPROMInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						if (this.ScanType == Constants.ScanType.SYNCING)
							this.MemberServerProfileInstance.UpdateFlagChanges = (this.MemberServerProfileInstance.UpdateFlagChanges | Utils.CLEAR_EX);

						if (this.ScanType == Constants.ScanType.ACTIVATION || (!this.clearMessagesOnly) )
						{
							this.UpdateSettingsFlag(Utils.USER_SETTINGS_BIT_LOC);
						}

					}
					else {
						this.Adapter.DisconnectDevice(this.Device);
					}
							   
					break;
				default:
					Debug.WriteLine("Invalid response received: " + this.Command.ToString());
					break;
			}
		}

		public void DescriptorWrite(object sender, EventArgs e)
		{
			Debug.WriteLine("Success writing descriptor.");
			ProcessCommands();
		}

		public void StartWriteSettings()
		{
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceSettings);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ClearEEProm);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteUserSettings);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteExerciseSettings);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteCompanySettings);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteSignatureSettings);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteSeizureSettings);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteScreenDisplay);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceSensitivity);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceStatus);

		}

		public void SetWebService(IWebServicesWrapper webservice)
		{
			this.WebService = webservice;
		}

		private void InitDataModelObjects()
		{
			UserSettingsInstance = new UserSettings(TrioDeviceInformationInstance);
			DeviceStatusInstance = new DeviceStatus(TrioDeviceInformationInstance);
			ClearEEPROMInstance = new ClearEEPROM(TrioDeviceInformationInstance);
			CompanySettingsInstance = new CompanySettings(TrioDeviceInformationInstance);
			DeviceSettingsInstance = new DeviceSettings(TrioDeviceInformationInstance);
			ExerciseSettingsInstance = new ExerciseSettings(TrioDeviceInformationInstance);
			SeizureSettingsInstance = new SeizureSettings(TrioDeviceInformationInstance);
			SensitivitySettingsInstance = new SensitivitySettings(TrioDeviceInformationInstance);
			DeviceModeInstance = new DeviceMode(TrioDeviceInformationInstance);
			SignatureSettingsInstance = new SignatureSettings(TrioDeviceInformationInstance);
			DisplayOnScreenInstance = new DisplayOnScreenData(TrioDeviceInformationInstance);
			WSActivateDeviceWithMemberInstance = new ActivateDeviceWithMember(TrioDeviceInformationInstance);
			FirmwareDisplaySequenceInstance = new FirmwareDisplaySequenceData(TrioDeviceInformationInstance);
			WSNotifySettingsUpdateInstance = new NotifySettingsUpdate();
			WSGetDeviceInformationInstance = new GetDeviceInformation(TrioDeviceInformationInstance);
			UploadTalliesDataInstance = new UploadTalliesData(TrioDeviceInformationInstance);
			TalliesDataInstance = new TalliesData(TrioDeviceInformationInstance);
			SeizureBlocksDataInstance = new SeizureBlocksData(TrioDeviceInformationInstance);
			WSUploadStepsDataInstance = new UploadStepsData();
			StepsTableDataInstance = new StepsTableData(TrioDeviceInformationInstance);
			StepsDataInstance = new StepsData(TrioDeviceInformationInstance);
			SignatureDataInstance = new SignatureData(TrioDeviceInformationInstance);
			WSUploadSignatureDataInstance = new UploadSignatureData();
			SeizureDataInstance = new SeizureData(TrioDeviceInformationInstance);
			WSUploadSeiureDataInstance = new UploadSeizureData();
			WSUnpairMemberDeviceInstance = new UnpairMemberDevice();
			WSUploadCommandResponseInstance = new UploadCommandResponse();
		}

		public bool ValidateActivationCode(string enteredCode)
		{
			/*String strGenerated = "";
			for (int i = 0; i < this.ActivationCode.Length; i++)
			{
				string temp = Convert.ToString(this.ActivationCode[i]);
				strGenerated.Insert(i,temp );
			}*/
			String strGenerated =  System.Text.Encoding.UTF8.GetString(this.ActivationCode, 0, this.ActivationCode.Length);
			return Utils.ValidateActivationCode(strGenerated, enteredCode);
		}

		public void SetUserInfo(UserInformation userInfo)
		{
			UserInformationInstance = userInfo;
		}

		void TimerForServer_Increment(object sender, EventArgs e)
		{
			this.ServerReadTimeDuration++;
			Debug.WriteLine(this.ServerReadTimeDuration);
		}

		void TimerForDevice_Increment(object sender, EventArgs e)
		{
			this.DeviceReadTimeDuration++;
			Debug.WriteLine(this.DeviceReadTimeDuration);
		}

		private void SetSettingsFromActivateDeviceWithMemberResponseWS(ActivateDeviceWithMemberResponse WSActivateDeviceWithMemberResponseInstance)
		{
			// Device Data Settings
			DeviceDataInstance = new DeviceData();
			DeviceDataInstance.AdvertiseMode = WSActivateDeviceWithMemberResponseInstance.AdvertiseMode;
			DeviceDataInstance.TrackerSensitivity = WSActivateDeviceWithMemberResponseInstance.TrackerSensitivity;
			this.ScreenFlowList = WSActivateDeviceWithMemberResponseInstance.ScreenFlow;

			//User Settings
			UserSettingsInstance.Stride = WSActivateDeviceWithMemberResponseInstance.userSettings.Stride;
			UserSettingsInstance.Weight = WSActivateDeviceWithMemberResponseInstance.userSettings.Weight;
			UserSettingsInstance.RestMetabolicRate = WSActivateDeviceWithMemberResponseInstance.userSettings.RestMetabolicRate;
			UserSettingsInstance.UnitOfMeasure = WSActivateDeviceWithMemberResponseInstance.userSettings.UnitOfMeasure;
			UserSettingsInstance.DOBYear = WSActivateDeviceWithMemberResponseInstance.userSettings.DOBYear;
			UserSettingsInstance.DOBMonth = WSActivateDeviceWithMemberResponseInstance.userSettings.DOBMonth;
			UserSettingsInstance.DOBDay = WSActivateDeviceWithMemberResponseInstance.userSettings.DOBDay;
			UserSettingsInstance.Age = WSActivateDeviceWithMemberResponseInstance.userSettings.Age;

			// Device Settings
			string serverTime = WSActivateDeviceWithMemberResponseInstance.ServerDateTime;
			bool timeFormat = WSActivateDeviceWithMemberResponseInstance.DeviceTimeFormat;

			DateTime st = Motion.Mobile.Utilities.Utils.GetServerDateTimeFromString(serverTime);

			// 
			this.timeDiff = WSActivateDeviceWithMemberResponseInstance.AllowableTimeDifference;


			Debug.WriteLine("Date Time" + st);
			DeviceSettingsInstance.Year = st.Year - 2000;
			DeviceSettingsInstance.Month = st.Month;
			DeviceSettingsInstance.Day = st.Day;
			DeviceSettingsInstance.Hour = st.Hour;
			DeviceSettingsInstance.Minute = st.Minute;
			DeviceSettingsInstance.Second = st.Second;
			DeviceSettingsInstance.HourType = timeFormat;

			this.clearTracker = WSActivateDeviceWithMemberResponseInstance.ClearTrackerDataFlag;

			//Company Settings
			CompanySettingsInstance.TenacitySteps = WSActivateDeviceWithMemberResponseInstance.companySettings.TenacitySteps;
			CompanySettingsInstance.IntensitySteps = WSActivateDeviceWithMemberResponseInstance.companySettings.IntensitySteps;
			CompanySettingsInstance.IntensityTime = WSActivateDeviceWithMemberResponseInstance.companySettings.IntensityTime;
			CompanySettingsInstance.IntensityMinuteThreshold = WSActivateDeviceWithMemberResponseInstance.companySettings.IntensityMinuteThreshold;
			CompanySettingsInstance.IntensityRestMinuteAllowed = WSActivateDeviceWithMemberResponseInstance.companySettings.IntensityRestMinuteAllowed;
			CompanySettingsInstance.IntensityCycle = WSActivateDeviceWithMemberResponseInstance.companySettings.IntensityCycle;
			CompanySettingsInstance.FrequencySteps = WSActivateDeviceWithMemberResponseInstance.companySettings.FrequencySteps;
			CompanySettingsInstance.FrequencyCycleTime = WSActivateDeviceWithMemberResponseInstance.companySettings.FrequencyCycleTime;
			CompanySettingsInstance.FrequencyCycle = WSActivateDeviceWithMemberResponseInstance.companySettings.FrequencyCycle;
			CompanySettingsInstance.FrequencyCycleInterval = WSActivateDeviceWithMemberResponseInstance.companySettings.FrequencyCycleInterval;


			//Exercise Settings
			ExerciseSettingsInstance.SyncTimeInterval = WSActivateDeviceWithMemberResponseInstance.exerciseSettings.SyncTimeInterval;
			ExerciseSettingsInstance.DataSyncEnable = WSActivateDeviceWithMemberResponseInstance.exerciseSettings.DataSyncEnable;
			ExerciseSettingsInstance.FrequencyAlarmEnable = WSActivateDeviceWithMemberResponseInstance.exerciseSettings.FrequencyAlarmEnable;

			//Signature Settings
			SignatureSettingsInstance.SamplingFrequency = WSActivateDeviceWithMemberResponseInstance.signatureSettings.SamplingFrequency;
			SignatureSettingsInstance.SamplingTime = WSActivateDeviceWithMemberResponseInstance.signatureSettings.SamplingTime;
			SignatureSettingsInstance.SamplingCycle = WSActivateDeviceWithMemberResponseInstance.signatureSettings.SamplingCycle;
			SignatureSettingsInstance.SamplingThreshold = WSActivateDeviceWithMemberResponseInstance.signatureSettings.SamplingThreshold;
			SignatureSettingsInstance.SamplingTotalBlocks = WSActivateDeviceWithMemberResponseInstance.signatureSettings.SamplingTotalBlocks;

			//Seizure Settings
			SeizureSettingsInstance.SeizureSettingsEnable = WSActivateDeviceWithMemberResponseInstance.seizureSettings.SeizureSettingsEnable;
			SeizureSettingsInstance.SeizureSamplingFrequency = WSActivateDeviceWithMemberResponseInstance.seizureSettings.SeizureSamplingFrequency;
			SeizureSettingsInstance.SeizureNumberOfRecords = WSActivateDeviceWithMemberResponseInstance.seizureSettings.SeizureNumberOfRecords;
			SeizureSettingsInstance.SeizureSamplingTime = WSActivateDeviceWithMemberResponseInstance.seizureSettings.SeizureSamplingTime;

		}

		private void SetSettingsFromGetDeviceInfoResponseWS(GetDeviceInfoResponse WSGetDeviceInfoResponseInstance)
		{
			

			AppUpdateSettingsInstance = new ApplicationUpdateSettings();
			AppUpdateSettingsInstance.UpdateFlag = WSGetDeviceInfoResponseInstance.appUpdateInfo.UpdateFlag;
			AppUpdateSettingsInstance.UpdateType = WSGetDeviceInfoResponseInstance.appUpdateInfo.UpdateType;
			AppUpdateSettingsInstance.VersionOfNewApp = WSGetDeviceInfoResponseInstance.appUpdateInfo.VersionOfNewApp;

			this.updateType = AppUpdateSettingsInstance.UpdateType;

			FirmwareUpdateInfoInstance = new FirmwareUpdateInfo();
			FirmwareUpdateInfoInstance.ModelFirmwareID = WSGetDeviceInfoResponseInstance.deviceFwUpdateInfo.ModelFirmwareID;
			FirmwareUpdateInfoInstance.VersionOfNewFirmware = WSGetDeviceInfoResponseInstance.deviceFwUpdateInfo.VersionOfNewFirmware;

			MemberServerProfileInstance = new MemberServerProfile();
			MemberServerProfileInstance.MemberDeviceID = WSGetDeviceInfoResponseInstance.MemberDeviceID;
			MemberServerProfileInstance.UpdateFlag = WSGetDeviceInfoResponseInstance.UpdateFlag;
			MemberServerProfileInstance.UpdateFlagChanges = 0;
			MemberServerProfileInstance.SynchronizationID = WSGetDeviceInfoResponseInstance.SynchronizationID;
			MemberServerProfileInstance.ServerDateTime = WSGetDeviceInfoResponseInstance.ServerDateTime;


			this.clearTracker = WSGetDeviceInfoResponseInstance.ClearTrackerDataFlag;


			ProcessSignatureUploaded(WSGetDeviceInfoResponseInstance.SignatureUploadedDates);
			if(WSGetDeviceInfoResponseInstance.lastSeizureDataUploadInfo != null)
				ProcessSeizureUploaded(WSGetDeviceInfoResponseInstance.lastSeizureDataUploadInfo.SeizureDate,WSGetDeviceInfoResponseInstance.lastSeizureDataUploadInfo.BlockNumber);


			//User Settings
			UserSettingsInstance.Stride = WSGetDeviceInfoResponseInstance.userSettings.Stride;
			UserSettingsInstance.Weight = WSGetDeviceInfoResponseInstance.userSettings.Weight;
			UserSettingsInstance.RestMetabolicRate = WSGetDeviceInfoResponseInstance.userSettings.RestMetabolicRate;
			UserSettingsInstance.UnitOfMeasure = WSGetDeviceInfoResponseInstance.userSettings.UnitOfMeasure;
			UserSettingsInstance.DOBYear = WSGetDeviceInfoResponseInstance.userSettings.DOBYear;
			UserSettingsInstance.DOBMonth = WSGetDeviceInfoResponseInstance.userSettings.DOBMonth;
			UserSettingsInstance.DOBDay = WSGetDeviceInfoResponseInstance.userSettings.DOBDay;
			UserSettingsInstance.Age = WSGetDeviceInfoResponseInstance.userSettings.Age;

			// Device Settings
			string serverTime = WSGetDeviceInfoResponseInstance.ServerDateTime;
			bool timeFormat = WSGetDeviceInfoResponseInstance.DeviceTimeFormat;

			DateTime st = Motion.Mobile.Utilities.Utils.GetServerDateTimeFromString(serverTime);

			//
			this.timeDiff = WSGetDeviceInfoResponseInstance.AllowableTimeDifference;


			Debug.WriteLine("Date Time" + st);
			DeviceSettingsInstance.Year = st.Year - 2000;
			DeviceSettingsInstance.Month = st.Month;
			DeviceSettingsInstance.Day = st.Day;
			DeviceSettingsInstance.Hour = st.Hour;
			DeviceSettingsInstance.Minute = st.Minute;
			DeviceSettingsInstance.Second = st.Second;
			DeviceSettingsInstance.HourType = timeFormat;



			//Company Settings
			CompanySettingsInstance.TenacitySteps = WSGetDeviceInfoResponseInstance.companySettings.TenacitySteps;
			CompanySettingsInstance.IntensitySteps = WSGetDeviceInfoResponseInstance.companySettings.IntensitySteps;
			CompanySettingsInstance.IntensityTime = WSGetDeviceInfoResponseInstance.companySettings.IntensityTime;
			CompanySettingsInstance.IntensityMinuteThreshold = WSGetDeviceInfoResponseInstance.companySettings.IntensityMinuteThreshold;
			CompanySettingsInstance.IntensityRestMinuteAllowed = WSGetDeviceInfoResponseInstance.companySettings.IntensityRestMinuteAllowed;
			CompanySettingsInstance.IntensityCycle = WSGetDeviceInfoResponseInstance.companySettings.IntensityCycle;
			CompanySettingsInstance.FrequencySteps = WSGetDeviceInfoResponseInstance.companySettings.FrequencySteps;
			CompanySettingsInstance.FrequencyCycleTime = WSGetDeviceInfoResponseInstance.companySettings.FrequencyCycleTime;
			CompanySettingsInstance.FrequencyCycle = WSGetDeviceInfoResponseInstance.companySettings.FrequencyCycle;
			CompanySettingsInstance.FrequencyCycleInterval = WSGetDeviceInfoResponseInstance.companySettings.FrequencyCycleInterval;


			//Exercise Settings
			ExerciseSettingsInstance.SyncTimeInterval = WSGetDeviceInfoResponseInstance.exerciseSettings.SyncTimeInterval;
			ExerciseSettingsInstance.DataSyncEnable = WSGetDeviceInfoResponseInstance.exerciseSettings.DataSyncEnable;
			ExerciseSettingsInstance.FrequencyAlarmEnable = WSGetDeviceInfoResponseInstance.exerciseSettings.FrequencyAlarmEnable;

			//Signature Settings
			SignatureSettingsInstance.SamplingFrequency = WSGetDeviceInfoResponseInstance.signatureSettings.SamplingFrequency;
			SignatureSettingsInstance.SamplingTime = WSGetDeviceInfoResponseInstance.signatureSettings.SamplingTime;
			SignatureSettingsInstance.SamplingCycle = WSGetDeviceInfoResponseInstance.signatureSettings.SamplingCycle;
			SignatureSettingsInstance.SamplingThreshold = WSGetDeviceInfoResponseInstance.signatureSettings.SamplingThreshold;
			SignatureSettingsInstance.SamplingTotalBlocks = WSGetDeviceInfoResponseInstance.signatureSettings.SamplingTotalBlocks;

			//Seizure Settings
			SeizureSettingsInstance.SeizureSettingsEnable = WSGetDeviceInfoResponseInstance.seizureSettings.SeizureSettingsEnable;
			SeizureSettingsInstance.SeizureSamplingFrequency = WSGetDeviceInfoResponseInstance.seizureSettings.SeizureSamplingFrequency;
			SeizureSettingsInstance.SeizureNumberOfRecords = WSGetDeviceInfoResponseInstance.seizureSettings.SeizureNumberOfRecords;
			SeizureSettingsInstance.SeizureSamplingTime = WSGetDeviceInfoResponseInstance.seizureSettings.SeizureSamplingTime;

		}

		private void ProcessSignatureUploaded(List<string> signatureList)
		{
			this.SignatureUploadedDatesList = new List<DateTime>();

			if (signatureList.Count > 0)
			{
				for (int i = 0; i < signatureList.Count; i++)
				{
					this.SignatureUploadedDatesList.Add(Motion.Mobile.Utilities.Utils.GetServerDateTimeFromString(signatureList[i]));
				}
				SignatureUploadedDatesList.Sort((a, b) => b.CompareTo(a));
			}
		}
		private void ProcessSeizureUploaded(string dateInString, int blockNo)
		{
			if (!string.Equals("", dateInString))
			{

				this.SeizureBlockInfoInstance = new SeizureBlockInfo();
				DateTime dt = Motion.Mobile.Utilities.Utils.GetServerDateTimeFromString(dateInString);
				this.SeizureBlockInfoInstance.sbYear = dt.Year - 2000;
				this.SeizureBlockInfoInstance.sbMonth = dt.Month;
				this.SeizureBlockInfoInstance.sbDay = dt.Day;
				this.SeizureBlockInfoInstance.sbHour = dt.Hour;
				this.SeizureBlockInfoInstance.sbMinute = dt.Minute;
				this.SeizureBlockInfoInstance.sbSeizureBlock = blockNo;

			}
		}

		private void AddSignatureToBeUploadedDates()
		{ 
			this.SignatureToBeUploadedTableList = new List<StepsTableParameters>();

			if (SignatureUploadedDatesList.Count > 0)
			{
				foreach (StepsTableParameters tableData in StepsTableDataInstance.stepsDataTable)
				{
					if (tableData.dbYear != 0 && tableData.dbMonth != 0 && tableData.dbDay != 0)
					{
						DateTime dtFromTable = Motion.Mobile.Utilities.Utils.GetDateFromDateComponentsV1(tableData.dbYear + 2000, tableData.dbMonth, tableData.dbDay);
						DateTime dtFromWebServerUnformatted = SignatureUploadedDatesList.ElementAt(0);
						DateTime dtFromWebServer = Motion.Mobile.Utilities.Utils.GetDateFromDateComponentsV1(dtFromWebServerUnformatted.Year, dtFromWebServerUnformatted.Month, dtFromWebServerUnformatted.Day);

						if (DateTime.Compare(dtFromTable, dtFromWebServer) > 0)
						{
							if (tableData.signatureGenerated == 1)
							{
								this.SignatureToBeUploadedTableList.Add(tableData);
							}

						}
					}
				}

			}
			else
			{
				foreach (StepsTableParameters tableData in StepsTableDataInstance.stepsDataTable)
				{
					if (tableData.signatureGenerated == 1)
					{
						this.SignatureToBeUploadedTableList.Add(tableData);
					}
				}

			}
		}

		private void CreateUploadStepsDataRequest(UploadStepsDataRequest WSUploadStepsDataRequestInstance)
		{
			DateTime dateTime = new DateTime(DeviceSettingsInstance.Year + 2000, DeviceSettingsInstance.Month, DeviceSettingsInstance.Day, DeviceSettingsInstance.Hour, DeviceSettingsInstance.Minute, DeviceSettingsInstance.Second);
			dateTime.AddSeconds(this.DeviceReadTimeDuration);

			WSUploadStepsDataRequestInstance.MemberID = 56375;
			WSUploadStepsDataRequestInstance.MemberDeviceID = 72277;
			WSUploadStepsDataRequestInstance.LastSyncSettingDateTime =(long) Motion.Mobile.Utilities.Utils.DateTimeToUnixTimestamp(DateTime.Now);
			WSUploadStepsDataRequestInstance.ApplicationID = 14487;
			WSUploadStepsDataRequestInstance.PlatformID = 1036;
			WSUploadStepsDataRequestInstance.MemberRMR = UserSettingsInstance.RestMetabolicRate;
			WSUploadStepsDataRequestInstance.MinuteDataType = 1;
			WSUploadStepsDataRequestInstance.HostName = "mactest";
			WSUploadStepsDataRequestInstance.TrackerFirmwareVersion = TrioDeviceInformationInstance.FirmwareVersion;
			WSUploadStepsDataRequestInstance.SyncType = TrioDeviceInformationInstance.BroadcastType;
			WSUploadStepsDataRequestInstance.BatteryLevel = TrioDeviceInformationInstance.BatteryLevel;
			WSUploadStepsDataRequestInstance.FrequencyMet = DeviceStatusInstance.FrequencyMet;
			WSUploadStepsDataRequestInstance.IntensityMet = DeviceStatusInstance.IntensityMet;
			WSUploadStepsDataRequestInstance.DeviceDateTime = Motion.Mobile.Utilities.Utils.GetDeviceDateTimeWithFormat(dateTime);
			WSUploadStepsDataRequestInstance.DataExtractDurationTime = this.stepDataReadTimeDuration * 1000;
			WSUploadStepsDataRequestInstance.TrackerSerialNumber = TrioDeviceInformationInstance.SerialNumber;
			WSUploadStepsDataRequestInstance.TrackerModel = TrioDeviceInformationInstance.ModelNumber;
			WSUploadStepsDataRequestInstance.SynchronizationID = MemberServerProfileInstance.SynchronizationID;
				 
		}

		private void CreateUploadSignatureRequestData(UploadSignatureDataRequest UploadSignatureDataRequestInstance)
		{ 
			DateTime dateTime = new DateTime(DeviceSettingsInstance.Year + 2000, DeviceSettingsInstance.Month, DeviceSettingsInstance.Day, DeviceSettingsInstance.Hour, DeviceSettingsInstance.Minute, DeviceSettingsInstance.Second);
			dateTime.AddSeconds(this.DeviceReadTimeDuration);

			UploadSignatureDataRequestInstance.MemberID = 56375;
			UploadSignatureDataRequestInstance.MemberDeviceID = 72277;
			UploadSignatureDataRequestInstance.LastSyncSettingDateTime = (long) Motion.Mobile.Utilities.Utils.DateTimeToUnixTimestamp(DateTime.Now);
			UploadSignatureDataRequestInstance.ApplicationID = 14487;
			UploadSignatureDataRequestInstance.PlatformID = 1036;
			UploadSignatureDataRequestInstance.HostName = "mactest";
			UploadSignatureDataRequestInstance.TrackerFirmwareVersion = TrioDeviceInformationInstance.FirmwareVersion;
			UploadSignatureDataRequestInstance.SyncType = TrioDeviceInformationInstance.BroadcastType;
			UploadSignatureDataRequestInstance.BatteryLevel = TrioDeviceInformationInstance.BatteryLevel;
			UploadSignatureDataRequestInstance.FrequencyMet = DeviceStatusInstance.FrequencyMet;
			UploadSignatureDataRequestInstance.IntensityMet = DeviceStatusInstance.IntensityMet;
			UploadSignatureDataRequestInstance.DeviceDateTime = Motion.Mobile.Utilities.Utils.GetDeviceDateTimeWithFormat(dateTime);
			UploadSignatureDataRequestInstance.DataExtractDurationTime = this.signDataReadTimeDuration * 1000;
			UploadSignatureDataRequestInstance.TrackerSerialNumber = TrioDeviceInformationInstance.SerialNumber;
			UploadSignatureDataRequestInstance.TrackerModel = TrioDeviceInformationInstance.ModelNumber;
			UploadSignatureDataRequestInstance.SynchronizationID = MemberServerProfileInstance.SynchronizationID;

		}

		private void CreateUploadSeizureRequestData(UploadSeizureDataRequest UploadSeizureDataRequestInstance)
		{

			DateTime dateTime = new DateTime(DeviceSettingsInstance.Year + 2000, DeviceSettingsInstance.Month, DeviceSettingsInstance.Day, DeviceSettingsInstance.Hour, DeviceSettingsInstance.Minute, DeviceSettingsInstance.Second);
			dateTime.AddSeconds(this.DeviceReadTimeDuration);

			UploadSeizureDataRequestInstance.MemberID = 56375;
			UploadSeizureDataRequestInstance.MemberDeviceID = 72277;
			UploadSeizureDataRequestInstance.LastSyncSettingDateTime =(long) Motion.Mobile.Utilities.Utils.DateTimeToUnixTimestamp(DateTime.Now);
			UploadSeizureDataRequestInstance.ApplicationID = 14487;
			UploadSeizureDataRequestInstance.PlatformID = 1036;
			UploadSeizureDataRequestInstance.HostName = "mactest";
			UploadSeizureDataRequestInstance.TrackerFirmwareVersion = TrioDeviceInformationInstance.FirmwareVersion;
			UploadSeizureDataRequestInstance.SyncType = TrioDeviceInformationInstance.BroadcastType;
			UploadSeizureDataRequestInstance.BatteryLevel = TrioDeviceInformationInstance.BatteryLevel;
			UploadSeizureDataRequestInstance.FrequencyMet = DeviceStatusInstance.FrequencyMet;
			UploadSeizureDataRequestInstance.IntensityMet = DeviceStatusInstance.IntensityMet;
			UploadSeizureDataRequestInstance.DeviceDateTime = Motion.Mobile.Utilities.Utils.GetDeviceDateTimeWithFormat(dateTime);
			UploadSeizureDataRequestInstance.DataExtractDurationTime = this.seizureDataReadTimeDuration * 1000;
			UploadSeizureDataRequestInstance.SignatureHeaderType = "E";
			UploadSeizureDataRequestInstance.SeizureBlockNo = SeizureDataInstance.BlockNumber + 1;
			UploadSeizureDataRequestInstance.TrackerSerialNumber = TrioDeviceInformationInstance.SerialNumber;
			UploadSeizureDataRequestInstance.TrackerModel = TrioDeviceInformationInstance.ModelNumber;
			UploadSeizureDataRequestInstance.SynchronizationID = MemberServerProfileInstance.SynchronizationID;

		}

		private void ReadStepsData()
		{
			bool isSendingCommand = false;
			for (int i = 0; i < StepsTableDataInstance.stepsDataTable.Count; i++)
			{
				StepsTableParameters stp = StepsTableDataInstance.stepsDataTable.ElementAt(i);
				Debug.WriteLine("Steps Date: " + stp.dbYear + " " + stp.dbMonth + " " + stp.dbDay + " " + stp.dbHourNumber);

				if (stp.dbYear != 0 && stp.dbMonth != 0 && stp.dbDay != 0)
				{
					if (!stp.allFlagged)
					{
						int currentHourValue = 0;
						for (int j = 0; j < stp.dbHourNumber; j++)
						{
							currentHourValue |= (int)Math.Pow(2, j);
							Debug.WriteLine("CurrentFlag Value " + currentHourValue + " sentHourFlag " + stp.sentHourFlag);
							if (stp.sentHourFlag == currentHourValue)
							{
								j += 1;

								if (stp.sentHourFlag > 0 && (j == stp.dbHourNumber))
								{
									isSendingCommand = true;
									this.isReadingCurrentHour = true;
									this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadCurrentHour);
									this.ProcessCommands();

								}

								else
								{
									isSendingCommand = true;
									this.currentDateForStepTable = stp.tableDate;
									this.currentStartTimeForStepTable = j;
									this.currentEndTimeForStepTable = stp.dbHourNumber;
									this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadHourlySteps);
									this.ProcessCommands();
								}

								break;
							}
						}

						if ((stp.dbHourNumber == 0 && stp.sentHourFlag == 0))
						{
							isSendingCommand = true;
							this.isReadingCurrentHour = true;
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadCurrentHour);
							this.ProcessCommands();
							break;

						}

					}
				}
			}

			if (!isSendingCommand)
			{
				this.isReadingCurrentHour = true;
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadCurrentHour);
				this.ProcessCommands();

			}
		}

		private StepsTableParameters UpdateStepDataTable()
		{

			if (this.isUnFlaggingTableHeaderDueToTimeChange)
			{

				return null;
			}

			else
			{ 
				StepsTableParameters tableDaily = null;
				bool tableDayFound = false;
				foreach (StepsDailyData daily in StepsDataInstance.dailyData)
				{
					foreach (StepsTableParameters tableData in StepsTableDataInstance.stepsDataTable)
					{
						if ((daily.stepsYear == tableData.dbYear) && (daily.stepsMonth == tableData.dbMonth) && (daily.stepsDay == tableData.dbDay))
						{
							if (tableData.allFlagged)
							{
								//found the date in the table but all hourly data is already flag as sent
								//This should not happen because each hour of the read daily step data is unflag
								//ignore and continue next iteration
							}

							else
							{
								for (int j = 0; j < tableData.dbHourNumber; j++)
								{
									tableData.sentHourFlag |= (int)Math.Pow(2, j);
								}

								tableData.allFlagged = tableData.sentHourFlag == 16777215 ? true : false;
								tableDayFound = true;
								tableDaily = tableData;
								break;
							}
						}
					}

					if (tableDayFound)
						break;
				}

				return tableDaily;
			}


		}

		private void ProcessWriteTableHeaderResponse()
		{
			if (StepsDataInstance.dailyData.Count > 0)
			{
				StepsDataInstance.dailyData.RemoveAt(0);

				StepTableParamInstance = this.UpdateStepDataTable();
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteStepsHeader);
				this.ProcessCommands();
			}

			else
			{ 
				bool hasDataToSync = false;
				bool shouldExitLoop = false;
				foreach (StepsTableParameters tableData in StepsTableDataInstance.stepsDataTable)
				{
					bool willContinueOuterLoop = true;

					if (!tableData.allFlagged)
					{
						hasDataToSync = true;
						if (tableData.dbYear != 0 && tableData.dbMonth != 0 && tableData.dbDay != 0)
						{
							int flagHourCount = 0;
							bool shouldStopCounter = false; 
							int currentHourValue = 0;
							for (int j = 0; j < tableData.dbHourNumber; j++)
							{
								currentHourValue |= (int)Math.Pow(2, j);
								//Debug.WriteLine("CurrentFlag Value " + currentHourValue + " sentHourFlag " + stp.sentHourFlag);
								if ((tableData.sentHourFlag == currentHourValue) || shouldStopCounter)
								{
									shouldStopCounter = true;
								}
								else
									flagHourCount++;
							}

							if (tableData.dbHourNumber == 0 && tableData.sentHourFlag == 0)
							{
								hasDataToSync = false;
							}
							else if (tableData.sentHourFlag > 0 && tableData.sentHourFlag == currentHourValue)
							{
								hasDataToSync = false;
							}
							else if (flagHourCount > 0 && tableData.dbHourNumber == 0)
							{
								hasDataToSync = false;
							}
							else if (flagHourCount > tableData.dbHourNumber)
							{
								hasDataToSync = false;
							}

							shouldExitLoop = true;
						}

					}

					if (shouldExitLoop)
						break;
				}

				if (!hasDataToSync)
				{
					this.isReadingCurrentHour = true;
					this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadCurrentHour);
					this.ProcessCommands();

					//self.stepsDataReadTimer TO DO

				}

				else
				{
					this.ReadStepsData();
				}
			}

		}

		private void checkTimeDifferenceAndUpdateDeviceTime()
		{
			DateTime serverDT = Motion.Mobile.Utilities.Utils.GetServerDateTimeFromString(this.MemberServerProfileInstance.ServerDateTime);
			DateTime deviceDT = new DateTime(DeviceSettingsInstance.Year + 2000, DeviceSettingsInstance.Month, DeviceSettingsInstance.Day, DeviceSettingsInstance.Hour, DeviceSettingsInstance.Minute, DeviceSettingsInstance.Second);

			Debug.WriteLine("Current ServerTime " + serverDT);
			Debug.WriteLine("Current DeviceTime " + deviceDT);

			Debug.WriteLine("Before ServerTimeDuration " + this.ServerReadTimeDuration);
			Debug.WriteLine("Before DeviceTimeDuration " + this.DeviceReadTimeDuration);

			if (this.timerForServer != null)
			{
				this.timerForServer.Cancel();
				this.timerForServer = null;
			}

			if (this.timerForDevice != null)
			{
				this.timerForDevice.Cancel();
				this.timerForDevice = null;
			}

			Debug.WriteLine("After ServerTimeDuration " + this.ServerReadTimeDuration);
			Debug.WriteLine("After DeviceTimeDuration " + this.DeviceReadTimeDuration);

			serverDT = serverDT.AddSeconds(this.ServerReadTimeDuration);
			deviceDT = deviceDT.AddSeconds(this.DeviceReadTimeDuration);

			this.MemberServerProfileInstance.ServerDateTime = Motion.Mobile.Utilities.Utils.GetStringDateV1(serverDT.Year, serverDT.Month, serverDT.Day, serverDT.Hour, serverDT.Minute, serverDT.Second);

			Debug.WriteLine("After Adding time Duration => ServerTime " + serverDT);
			Debug.WriteLine("After Adding time Duration => DeviceTime " + deviceDT);


			double timeDiffInSeconds = (serverDT - deviceDT).TotalSeconds;
			double timeDiffInMins = timeDiffInSeconds / 60.0f;

			//this.updateTimeChangeStepsTableDataArrayFromDate(serverDT, deviceDT);
			int timeDiffInMinsAbs = (int)Math.Abs(timeDiffInMins);
			if (timeDiffInMinsAbs >= this.timeDiff)
			{
				//Check if new member timezone hour is less than current device hour
				if (timeDiffInMins <= -59)
				{
					this.isUnFlaggingTableHeaderDueToTimeChange = true;
					if (serverDT.Year < deviceDT.Year)
						this.updateTimeChangeStepsTableDataArrayFromDate(serverDT, deviceDT);
					else if ((serverDT.Year == deviceDT.Year) && (serverDT.Month < deviceDT.Month))
						this.updateTimeChangeStepsTableDataArrayFromDate(serverDT, deviceDT);
					else if ((serverDT.Year == deviceDT.Year) && (serverDT.Month == deviceDT.Month) && (serverDT.Day < deviceDT.Day))
						this.updateTimeChangeStepsTableDataArrayFromDate(serverDT, deviceDT);
					else
						this.updateTimeChangeStepsTableDataArrayWithDate(serverDT);
				}

				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceSettings);
				this.ProcessCommands();
			}

			else
			{ 
				
			}
		}

		private void updateTimeChangeStepsTableDataArrayFromDate(DateTime newServerDate, DateTime oldDeviceDate)
		{
			int numberOfDates = 2;
			for (int i = 0; i < numberOfDates; i++)
			{
				DateTime date = i == 0 ? newServerDate : oldDeviceDate;
				int year = date.Year - 2000;
				foreach (StepsTableParameters tableData in StepsTableDataInstance.stepsDataTable)
				{
					if ((tableData.dbYear == year) && (tableData.dbMonth == date.Month) && (tableData.dbDay == date.Day))
					{
						tableData.dbYear = year;
						tableData.dbMonth = date.Month;
						tableData.dbDay = date.Day;
						tableData.sentHourFlag = 0;

						if (i == 0)
						{ 
							for (int j = 1; j <= date.Hour; j++)
							{
								tableData.sentHourFlag |= (int)Math.Pow(2, j);
							}
						}

						tableData.allFlagged = tableData.sentHourFlag == 16777215 ? true : false;
						this.TimeChangeTableDataList.Add(tableData);
						break;
					}
				}
			}
		}

		private void updateTimeChangeStepsTableDataArrayWithDate(DateTime referenceDate)
		{ 
			
			int year = referenceDate.Year - 2000;
			foreach (StepsTableParameters tableData in StepsTableDataInstance.stepsDataTable)
			{
				if ((tableData.dbYear == year) && (tableData.dbMonth == referenceDate.Month) && (tableData.dbDay == referenceDate.Day))
				{
					tableData.dbYear = year;
					tableData.dbMonth = referenceDate.Month;
					tableData.dbDay = referenceDate.Day;
					tableData.sentHourFlag = 0;

					for (int j = 1; j <= referenceDate.Hour; j++)
					{
						tableData.sentHourFlag |= (int)Math.Pow(2, j);
					}

					tableData.allFlagged = tableData.sentHourFlag == 16777215 ? true : false;
					this.TimeChangeTableDataList.Add(tableData);
					break;
				}
			}
		}

		private void CheckSeizureOrSignatureForUpload()
		{
			if (this.SeizureToBeUploadedList.Count() > 0)
			{
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSeizure);
				this.ProcessCommands();
			}
			else
			{
				if (this.SignatureToBeUploadedTableList.Count() > 0)
				{ 
					this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSignature);
					this.ProcessCommands();
				}
			}
				
		}

		private void UpdateSettingsFlag(int bitLocValue)
		{
			long updateSettings = (this.MemberServerProfileInstance.UpdateFlag >> bitLocValue) & 0x01;

			switch (bitLocValue)
			{

				case Utils.USER_SETTINGS_BIT_LOC:
					if (updateSettings > 0)
					{
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteUserSettings);
						this.ProcessCommands();
					}
					else
					{ 
						this.UpdateSettingsFlag(Utils.EXERCISE_SETTINGS_BIT_LOC);
					}
					break;
				case Utils.EXERCISE_SETTINGS_BIT_LOC:
					if (updateSettings > 0)
					{
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteExerciseSettings);
						this.ProcessCommands();
					}
					else
					{
						this.UpdateSettingsFlag(Utils.COMPANY_SETTINGS_BIT_LOC);
					}
					break;
				case Utils.COMPANY_SETTINGS_BIT_LOC:
					if (updateSettings > 0)
					{
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteCompanySettings);
						this.ProcessCommands();
					}
					else
					{
						this.UpdateSettingsFlag(Utils.SIGNATURE_SETTINGS_BIT_LOC);
					}
					break;	
				case Utils.SIGNATURE_SETTINGS_BIT_LOC:
					if (updateSettings > 0)
					{
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteSignatureSettings);
						this.ProcessCommands();
					}
					else
					{
						this.UpdateSettingsFlag(Utils.SEIZURE_SETTINGS_BIT_LOC);
					}
					break;
				case Utils.SEIZURE_SETTINGS_BIT_LOC:
					if (updateSettings > 0)
					{
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteSeizureSettings);
						this.ProcessCommands();
					}
					else
					{
						this.UpdateSettingsFlag(Utils.SCREEN_SETTINGS_BIT_LOC);
					}
					break;	
				case Utils.SCREEN_SETTINGS_BIT_LOC:
					if (updateSettings > 0)
					{
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteScreenFlow);
						this.ProcessCommands();
					}
					else
					{
						this.UpdateSettingsFlag(Utils.SENSITIVITY_SETTINGS_BIT_LOC);
					}
					break;
				case Utils.SENSITIVITY_SETTINGS_BIT_LOC:
					if (updateSettings > 0)
					{
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceSensitivity);
						this.ProcessCommands();
					}
					else
					{
						if ((this.MemberServerProfileInstance.UpdateFlagChanges & 0x3FF) > 0)
						{
							this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsSendNotifySettingsUpdate);
							this.ProcessCommands();
						}
						else
						{ 
							this.UpdateSettingsFlag(Utils.MESSAGE_SETTINGS_BIT_LOC);
						}
							
					}
					break;	
				case Utils.MESSAGE_SETTINGS_BIT_LOC:
					if (updateSettings > 0)
					{
						// Get messages from webserver
					}
					else
					{
						this.UpdateSettingsFlag(Utils.CLEAR_MESSAGES_SETTINGS_BIT_LOC);

					}
					break;
				case Utils.CLEAR_MESSAGES_SETTINGS_BIT_LOC:
					if (updateSettings > 0)
					{
						this.clearMessagesOnly = true;
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ClearEEProm);
						this.ProcessCommands();
					}

					break;
				default:
					break;	
					
			}

		}


		private void UploadSignatureData()
		{ 
			
		}

	}
}

