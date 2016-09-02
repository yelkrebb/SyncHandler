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
using Motion.Core.Data.WebServiceData.DeviceWebServices;
using Motion.Core.WSHandler;
using Motion.Mobile.Core.BLE;
using Motion.Mobile.Utilities;
using Motion.Core.Data.UserData;
using Motion.Core.Data.WebServiceData;

namespace Motion.Core.SyncHandler
{
	public class SyncDeviceHandler961 : ISyncDeviceHandler
	{
		public UserInformation UserInformationInstance { get; set; }

		private static readonly object _synclock = new object();
		private static SyncDeviceHandler961 Instance;


		public event EventHandler IncrementProgressBar = delegate { };
		public event EventHandler<SyncDoneEventArgs> SyncDone = delegate { };


		private IAdapter Adapter;
		private IDevice Device;
		private IWebServicesWrapper WebService;

		private ICharacteristic Ff07Char;
		private ICharacteristic Ff08Char;
		private ICharacteristic ReadChar;

		private TrioDeviceInformation TrioDeviceInformationInstance;


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


		private ActivateDeviceWithMember ActivateDeviceWithMemberInstance;
		private ActivateDeviceWithMemberResponse ActivateDeviceWithMemberResponseInstance;


		private Queue<Constants.SyncHandlerSequence> ProcessQeueue = new Queue<Constants.SyncHandlerSequence>();
		private Constants.SyncHandlerSequence Command;
		private EventHandler<CharacteristicReadEventArgs> NotifyStateUpdated = null;
		public event EventHandler<StatusEventArgs> FourDigitCodeChecked = delegate { };

		private Constants.ScanType ScanType;
		private BLEParsingStatus ParsingStatus;

		private byte[] CommandRequest;
		private byte[] ActivationCode;

		//flag for progress increment eligibility
		//value will only be true if device configuration is done 
		//as well as the reading of necessary characteristic values are also done.
		private Boolean StartIncrementProgress;

		private List<byte[]> PacketsReceived = new List<byte[]>();

		private SyncDeviceHandler961()
		{
			
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
		}

		public void StartSync(Constants.ScanType scanType)
		{
			this.ScanType = scanType;
			this.ProcessQeueue.Clear();
			this.StartIncrementProgress = false;

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
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ActivateDeviceWithMember);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteDeviceSettings);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ClearEEProm);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteUserSettings);



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



				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadDeviceSettings);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WsGetDeviceInfo);


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
				case Constants.SyncHandlerSequence.ReadSeizureTable:
					Debug.WriteLine("SyncDeviceHandler961: Read Seizure Table");
					break;
				case Constants.SyncHandlerSequence.ReadHourlySteps:
					Debug.WriteLine("SyncDeviceHandler961: Read Steps Header");
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadCurrentHour:
					Debug.WriteLine("SyncDeviceHandler961: Read Current Hour");
					CommandRequest = new byte[] { 0x1B, 0x27 };
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadSignature:
					Debug.WriteLine("SyncDeviceHandler961: Read Signature Data");
					//CommandRequest = new byte[] { 0x1B, 0x27 };
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadSeizure:
					Debug.WriteLine("SyncDeviceHandler961: Read seizure Data");
					break;
				case Constants.SyncHandlerSequence.WriteStepsHeader:
					Debug.WriteLine("SyncDeviceHandler961: Writing steps header");
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
					break;
				case Constants.SyncHandlerSequence.WriteCompanySettings:
					Debug.WriteLine("SyncDeviceHandler961: Writing company settings");
					break;
				case Constants.SyncHandlerSequence.WriteSignatureSettings:
					Debug.WriteLine("SyncDeviceHandler961: Writing signature settings");
					break;
				case Constants.SyncHandlerSequence.WriteSeizureSettings:
					Debug.WriteLine("SyncDeviceHandler961: Writing seizure settings");
		          	break;
				case Constants.SyncHandlerSequence.WriteScreenFlow:
					Debug.WriteLine("SyncDeviceHandler961: Writing screenflow settings");
					break;
				case Constants.SyncHandlerSequence.WriteDeviceSensitivity:
					Debug.WriteLine("SyncDeviceHandler961: Writing device sensitivity settings");
					break;
				case Constants.SyncHandlerSequence.WriteDeviceStatus:
					Debug.WriteLine("SyncDeviceHandler961: Writing device status settings");
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
					ClearEEPROMInstance.ExerciseData = true;
					ClearEEPROMInstance.SettingsData = false;
					ClearEEPROMInstance.SurveyQuestions = true;
					ClearEEPROMInstance.Tallies = true;
					ClearEEPROMInstance.MedicineAlarm = true;
					ClearEEPROMInstance.MotivationalQuotes = true;
					CommandRequest = await ClearEEPROMInstance.GetWriteCommand();
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;

				case Constants.SyncHandlerSequence.ActivateDeviceWithMember:
					Debug.WriteLine("SyncDeviceHandler961: WS Activate Device With Member");
					ActivateDeviceWithMemberInstance.request.TrackerModel = TrioDeviceInformationInstance.ModelNumber;
					ActivateDeviceWithMemberInstance.request.TrackerSerialNumber = TrioDeviceInformationInstance.SerialNumber;
					ActivateDeviceWithMemberInstance.request.TrackerBtAdd = "";
					ActivateDeviceWithMemberInstance.request.ApplicationID = 14487;
					ActivateDeviceWithMemberInstance.request.MemberDermID = 66525;
					ActivateDeviceWithMemberInstance.request.TrackerFirmwareVersion = 4.3f;
					WebServiceRequestStatus status = await ActivateDeviceWithMemberInstance.PostRequest();

					if (status == WebServiceRequestStatus.SUCCESS)
					{
						Debug.WriteLine("Success");
						ActivateDeviceWithMemberResponseInstance = ActivateDeviceWithMemberInstance.response;
						if (ActivateDeviceWithMemberResponseInstance != null)
							this.SetSettingsFromWebServer();
						else
							    
							Debug.WriteLine("ActivateDeviceWithMemeber Response is null");
						ProcessCommands();
					}
					//this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.WsGetDeviceInfo:
					Debug.WriteLine("SyncDeviceHandler961: WS Request GetDeviceInfo");

					//to be replaced - start
					/*Dictionary<String, object> parameter = new Dictionary<String, object>();
					parameter.Add("serno", "9999999894");
					parameter.Add("fwver", "4.3");
					parameter.Add("mdl", "961");
					parameter.Add("aid", 16781);
					parameter.Add("ddtm", "16-08-12 14:15");
					Dictionary<string, object> dictionary = await WebService.PostData("https://test.savvysherpa.com/DeviceServices/api/Pedometer/GetDeviceInfo", parameter);
					//to be replaced - end
					string status = null;
					if (dictionary.ContainsKey("status"))
					{
						status = dictionary["status"].ToString();
					}

					if (status != null && status.ToUpper().CompareTo("OK") == 0)
					{
						Debug.WriteLine("Device is paired");
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadTallies);
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadDeviceStatus);
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadUserSettings);
						this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadStepsHeader);
						this.ProcessCommands();
					}
					else {
						Debug.WriteLine("Device is not paired");
						this.Adapter.DisconnectDevice(this.Device);
					}*/
					// set to true once getdevice info has succeeded
					//this.StartIncrementProgress = true;
					break;
				case Constants.SyncHandlerSequence.WsUploadTallies:
					break;
				case Constants.SyncHandlerSequence.WsUploadSteps:
					break;
				case Constants.SyncHandlerSequence.WsUploadSignature:
					break;
				case Constants.SyncHandlerSequence.WsUploadProfile:
					break;
				case Constants.SyncHandlerSequence.WsUnpairDevice:
					break;
				case Constants.SyncHandlerSequence.WsUploadSeizure:
					break;
				case Constants.SyncHandlerSequence.WsSendNotifySettingsUpdate:
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
						this.ProcessCommands();
					}
					break;
				case Constants.SyncHandlerSequence.ReadDeviceInformation:
					Debug.WriteLine("Receving device information");
					this.ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadDeviceStatus:
					Debug.WriteLine("Receving device status");
					this.ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadDeviceSettings:
					Debug.WriteLine("Receiving device settings");
					this.ParsingStatus = await DeviceSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.ProcessCommands();
					}
					else {
						this.Adapter.DisconnectDevice(this.Device);
					}
					this.ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadUserSettings:
					Debug.WriteLine("Receiving user settings: ");
					this.ParsingStatus = await UserSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						Debug.WriteLine("User RMR: " + UserSettingsInstance.RestMetabolicRate);
						this.ProcessCommands();
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
						for (int i = 0; i < this.PacketsReceived.Count; i++)
						{
							Array.Copy(this.PacketsReceived.ElementAt(i).ToArray(), Constants.INDEX_ZERO, data, (i * Constants.PACKET_BYTE_SIZE), Constants.PACKET_BYTE_SIZE);
						}
						this.ParsingStatus = await StepsTableDataInstance.ParseData(data);

						if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
						{
							for (int i = 0; i < StepsTableDataInstance.stepsDataTable.Count; i++)
							{
								StepsTableParameters stp = StepsTableDataInstance.stepsDataTable.ElementAt(i);
								Debug.WriteLine("Steps Date: " + stp.dbYear + " " + stp.dbMonth + " " + stp.dbDay + " " + stp.dbHourNumber);
							}
						}
						else {
							this.Adapter.DisconnectDevice(this.Device);
						}
					}
					break;
				case Constants.SyncHandlerSequence.ReadSeizureTable:
					Debug.WriteLine("Receiving seizure table data");
					this.PacketsReceived.Add(e.Data);
					if (Utils.LastPacketReceived(2, e.Data))
					{
						Debug.WriteLine("Last packet received...");
						//Process Data Here...
						this.ProcessCommands();
					}
					break;
				case Constants.SyncHandlerSequence.ReadHourlySteps:
					Debug.WriteLine("Receiving hourly steps data");
					this.PacketsReceived.Add(e.Data);
					if (Utils.TerminatorFound(0xFF, 4, e.Data))
					{
						Debug.WriteLine("Terminator Found...");
						this.ProcessCommands();
					}
					break;
				case Constants.SyncHandlerSequence.ReadCurrentHour:
					Debug.WriteLine("Receiving current hour steps data");
					this.PacketsReceived.Add(e.Data);
					if (Utils.TerminatorFound(0xFF, 4, e.Data))
					{
						Debug.WriteLine("Terminator Found...");
						this.ProcessCommands();
					}
					break;
				case Constants.SyncHandlerSequence.ReadSignature:
					Debug.WriteLine("Receiving signature data");
					this.PacketsReceived.Add(e.Data);
					if (Utils.TerminatorFound(0xFF, 4, e.Data))
					{
						Debug.WriteLine("Terminator Found...");
						this.ProcessCommands();
					}
					break;
				case Constants.SyncHandlerSequence.ReadSeizure:
					break;
				case Constants.SyncHandlerSequence.WriteStepsHeader:
					break;
				case Constants.SyncHandlerSequence.WriteDeviceSettings:
					this.ParsingStatus = await DeviceSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.ProcessCommands();
					}
					break;
				case Constants.SyncHandlerSequence.WriteUserSettings:
					this.ParsingStatus = await UserSettingsInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.ProcessCommands();
					}
					break;
				case Constants.SyncHandlerSequence.WriteExerciseSettings:
					break;
				case Constants.SyncHandlerSequence.WriteCompanySettings:
					break;
				case Constants.SyncHandlerSequence.WriteSignatureSettings:
					break;
				case Constants.SyncHandlerSequence.WriteSeizureSettings:
					break;
				case Constants.SyncHandlerSequence.WriteScreenFlow:
					break;
				case Constants.SyncHandlerSequence.WriteDeviceSensitivity:
					break;
				case Constants.SyncHandlerSequence.WriteDeviceStatus:
					break;
				case Constants.SyncHandlerSequence.ClearEEProm:
					Debug.WriteLine("Receiving clear eeprom: ");
					this.ParsingStatus = await ClearEEPROMInstance.ParseData(e.Data);
					if (this.ParsingStatus == BLEParsingStatus.SUCCESS)
					{
						this.ProcessCommands();
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
			//StepsTableDataInstance = new StepsTableData(TrioDeviceInformationInstance);
			ClearEEPROMInstance = new ClearEEPROM(TrioDeviceInformationInstance);
			CompanySettingsInstance = new CompanySettings(TrioDeviceInformationInstance);
			DeviceSettingsInstance = new DeviceSettings(TrioDeviceInformationInstance);
			ExerciseSettingsInstance = new ExerciseSettings(TrioDeviceInformationInstance);
			SeizureSettingsInstance = new SeizureSettings(TrioDeviceInformationInstance);
			SensitivitySettingsInstance = new SensitivitySettings(TrioDeviceInformationInstance);
			DeviceModeInstance = new DeviceMode(TrioDeviceInformationInstance);
			SignatureSettingsInstance = new SignatureSettings(TrioDeviceInformationInstance);
			DisplayOnScreenInstance = new DisplayOnScreenData(TrioDeviceInformationInstance);
			ActivateDeviceWithMemberInstance = new ActivateDeviceWithMember(TrioDeviceInformationInstance);
		}

		public bool ValidateActivationCode(string enteredCode)
		{
			/*String strGenerated = "";
			for (int i = 0; i < this.ActivationCode.Length; i++)
			{
				string temp = Convert.ToString(this.ActivationCode[i]);
				strGenerated.Insert(i,temp );
			}*/
			String strGenerated =  System.Text.Encoding.UTF8.GetString(this.ActivationCode
			                                                           , 0, this.ActivationCode.Length);
			return Utils.ValidateActivationCode(strGenerated, enteredCode);
		}

		public void SetUserInfo(UserInformation userInfo)
		{
			UserInformationInstance = userInfo;
		}

		private void SetSettingsFromWebServer()
		{

			//User Settings
			UserSettingsInstance.Stride = ActivateDeviceWithMemberResponseInstance.userSettings.Stride;
			UserSettingsInstance.Weight = ActivateDeviceWithMemberResponseInstance.userSettings.Weight;
			UserSettingsInstance.RestMetabolicRate = ActivateDeviceWithMemberResponseInstance.userSettings.RestMetabolicRate;
			UserSettingsInstance.UnitOfMeasure = ActivateDeviceWithMemberResponseInstance.userSettings.UnitOfMeasure;
			UserSettingsInstance.DOBYear = ActivateDeviceWithMemberResponseInstance.userSettings.DOBYear;
			UserSettingsInstance.DOBMonth = ActivateDeviceWithMemberResponseInstance.userSettings.DOBMonth;
			UserSettingsInstance.DOBDay = ActivateDeviceWithMemberResponseInstance.userSettings.DOBDay;
			UserSettingsInstance.Age = ActivateDeviceWithMemberResponseInstance.userSettings.Age;

			// Device Settings
			string serverTime = ActivateDeviceWithMemberResponseInstance.ServerDateTime;
			bool timeFormat = ActivateDeviceWithMemberResponseInstance.DeviceTimeFormat;

			DateTime st = Motion.Mobile.Utilities.Utils.GetServerDateTimeFromString(serverTime);


			Debug.WriteLine("Date Time" + st);
			DeviceSettingsInstance.Year = st.Year;
			DeviceSettingsInstance.Month = st.Month;
			DeviceSettingsInstance.Day = st.Day;
			DeviceSettingsInstance.Hour = st.Hour;
			DeviceSettingsInstance.Minute = st.Minute;
			DeviceSettingsInstance.Second = st.Second;
			DeviceSettingsInstance.HourType = timeFormat;


			//Company Settings
			CompanySettingsInstance.TenacitySteps = ActivateDeviceWithMemberResponseInstance.companySettings.TenacitySteps;
			CompanySettingsInstance.IntensitySteps = ActivateDeviceWithMemberResponseInstance.companySettings.IntensitySteps;
			CompanySettingsInstance.IntensityTime = ActivateDeviceWithMemberResponseInstance.companySettings.IntensityTime;
			CompanySettingsInstance.IntensityMinuteThreshold = ActivateDeviceWithMemberResponseInstance.companySettings.IntensityMinuteThreshold;
			CompanySettingsInstance.IntensityRestMinuteAllowed = ActivateDeviceWithMemberResponseInstance.companySettings.IntensityRestMinuteAllowed;
			CompanySettingsInstance.IntensityCycle = ActivateDeviceWithMemberResponseInstance.companySettings.IntensityCycle;
			CompanySettingsInstance.FrequencySteps = ActivateDeviceWithMemberResponseInstance.companySettings.FrequencySteps;
			CompanySettingsInstance.FrequencyCycleTime = ActivateDeviceWithMemberResponseInstance.companySettings.FrequencyCycleTime;
			CompanySettingsInstance.FrequencyCycle = ActivateDeviceWithMemberResponseInstance.companySettings.FrequencyCycle;
			CompanySettingsInstance.FrequencyCycleInterval = ActivateDeviceWithMemberResponseInstance.companySettings.FrequencyCycleInterval;

			//Exercise Settings
			ExerciseSettingsInstance.SyncTimeInterval = ActivateDeviceWithMemberResponseInstance.exerciseSettings.SyncTimeInterval;
			ExerciseSettingsInstance.DataSyncEnable = ActivateDeviceWithMemberResponseInstance.exerciseSettings.DataSyncEnable;
			ExerciseSettingsInstance.FrequencyAlarmEnable = ActivateDeviceWithMemberResponseInstance.exerciseSettings.FrequencyAlarmEnable;

			//Signature Settings
			SignatureSettingsInstance.SamplingFrequency = ActivateDeviceWithMemberResponseInstance.signatureSettings.SamplingFrequency;
			SignatureSettingsInstance.SamplingTime = ActivateDeviceWithMemberResponseInstance.signatureSettings.SamplingTime;
			SignatureSettingsInstance.SamplingCycle = ActivateDeviceWithMemberResponseInstance.signatureSettings.SamplingCycle;
			SignatureSettingsInstance.SamplingThreshold = ActivateDeviceWithMemberResponseInstance.signatureSettings.SamplingThreshold;
			SignatureSettingsInstance.SamplingTotalBlocks = ActivateDeviceWithMemberResponseInstance.signatureSettings.SamplingTotalBlocks;

			//Seizure Settings
			SeizureSettingsInstance.SeizureSettingsEnable = ActivateDeviceWithMemberResponseInstance.seizureSettings.SeizureSettingsEnable;
			SeizureSettingsInstance.SeizureSamplingFrequency = ActivateDeviceWithMemberResponseInstance.seizureSettings.SeizureSamplingFrequency;
			SeizureSettingsInstance.SeizureNumberOfRecords = ActivateDeviceWithMemberResponseInstance.seizureSettings.SeizureNumberOfRecords;
			SeizureSettingsInstance.SeizureSamplingTime = ActivateDeviceWithMemberResponseInstance.seizureSettings.SeizureSamplingTime;

		}
	}
}

