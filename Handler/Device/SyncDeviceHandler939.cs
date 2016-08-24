using System;
using System.Collections.Generic;
using System.Diagnostics;
using Motion.Core.WSHandler;
using Motion.Mobile.Core.BLE;
using Motion.Mobile.Utilities;

namespace Motion.Core.SyncHandler
{
	public class SyncDeviceHandler939 : ISyncDeviceHandler
	{
		private static readonly object _synclock = new object();
		private static SyncDeviceHandler939 Instance;

		public event EventHandler IncrementProgressBar = delegate { };
		public event EventHandler<SyncDoneEventArgs> SyncDone = delegate { };

		private IAdapter Adapter;
		private IDevice Device;
		private IWebServicesWrapper WebService;

		private ICharacteristic Ff07Char;
		private ICharacteristic Ff08Char;
		private ICharacteristic ReadChar;

		private Queue<Constants.SyncHandlerSequence> ProcessQeueue = new Queue<Constants.SyncHandlerSequence>();
		private Constants.SyncHandlerSequence Command;
		private EventHandler<CharacteristicReadEventArgs> NotifyStateUpdated = null;

		private Constants.ScanType ScanType;

		private byte[] CommandRequest;

		//flag for progress increment eligibility
		//value will only be true if device configuration is done 
		//as well as the reading of necessary characteristic values are also done.
		private Boolean StartIncrementProgress;

		private List<byte[]> PacketsReceived = new List<byte[]>();

		private SyncDeviceHandler939()
		{
			
		}

		public static SyncDeviceHandler939 GetInstance()
		{
			if (Instance == null)
			{
				lock (_synclock)
				{
					if (Instance == null)
					{
						Instance = new SyncDeviceHandler939();
					}
				}
			}

			return Instance;
		}

		public void CleanUp()
		{
			this.ProcessQeueue.Clear();
			this.Command = Constants.SyncHandlerSequence.Default;
			this.CommandRequest = new byte[] { };
			this.PacketsReceived.Clear();
			this.Device = null;
			this.StartIncrementProgress = false;
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
					this.Adapter.CommandResponse += ReceiveResponse;
					break;
				default:
					Debug.WriteLine("Unknown response!");
					break;

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
				Debug.WriteLine("No more commands to be processed!");
				return;
			}

			ICharacteristic chr = null;

			switch (Command)
			{
				case Constants.SyncHandlerSequence.EnableFF07:
					Debug.WriteLine("SyncDeviceHandler939: Enabling FF07 characteristic");
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
					Debug.WriteLine("SyncDeviceHandler939: Enabling FF08 characteristic");
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
					Debug.WriteLine("SyncDeviceHandler939: Reading model from characteristic.");
					ReadChar = GetServicesCharacteristic(Constants.CharacteristicsUUID._2A24);
					chr = await ReadChar.ReadAsync();
					Debug.WriteLine("Model: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadSerial:
					Debug.WriteLine("SyncDeviceHandler939: Reading serial from characterisitic.");
					ReadChar = GetServicesCharacteristic(Constants.CharacteristicsUUID._2A25);
					chr = await ReadChar.ReadAsync();
					Debug.WriteLine("Serial: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadFwVersion:
					Debug.WriteLine("SyncDeviceHandler939: Reading fw version from characteristic.");
					ReadChar = GetServicesCharacteristic(Constants.CharacteristicsUUID._2A26);
					chr = await ReadChar.ReadAsync();
					Debug.WriteLine("Firmware Version: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadManufacturer:
					Debug.WriteLine("SyncDeviceHandler939: Reading manufacturer from characteristic.");
					this.StartIncrementProgress = true;
					ReadChar = GetServicesCharacteristic(Constants.CharacteristicsUUID._2A29);
					chr = await ReadChar.ReadAsync();
					Debug.WriteLine("Manufacturer: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadBatteryLevel:
					Debug.WriteLine("SyncDeviceHandler939: Reading battery level from characteristic.");
					ReadChar = GetServicesCharacteristic(Constants.CharacteristicsUUID._2A19);
					chr = await ReadChar.ReadAsync();
					Debug.WriteLine("Battery Level: " + (int)chr.Value[0]);
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadDeviceStatus:
					Debug.WriteLine("SyncDeviceHandler939: Read Device Status");
					CommandRequest = new byte[] { 0x1B, 0x13 };
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadDeviceSettings:
					Debug.WriteLine("SyncDeviceHandler939: Read Device Settings");
					CommandRequest = new byte[] { 0x1B, 0x17 };
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadUserSettings:
					Debug.WriteLine("SyncDeviceHandler939: Read User Settings");
					CommandRequest = new byte[] { 0x1B, 0x19 };
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadStepsHeader:
					Debug.WriteLine("SyncDeviceHandler939: Read Steps Header");
					CommandRequest = new byte[] { 0x1B, 0x22 };
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadSeizureTable:
					Debug.WriteLine("SyncDeviceHandler939: Read Seizure Table");
					break;
				case Constants.SyncHandlerSequence.ReadHourlySteps:
					Debug.WriteLine("SyncDeviceHandler939: Read Steps Header");
					//CommandRequest = new byte[] { 0x1B, 0x24, 0x };
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadCurrentHour:
					Debug.WriteLine("SyncDeviceHandler939: Read Current Hour");
					CommandRequest = new byte[] { 0x1B, 0x27 };
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadSignature:
					Debug.WriteLine("SyncDeviceHandler939: Read Signature Data");
					//CommandRequest = new byte[] { 0x1B, 0x27 };
					this.Adapter.SendCommand(Ff07Char, CommandRequest);
					break;
				case Constants.SyncHandlerSequence.ReadSeizure:
					Debug.WriteLine("SyncDeviceHandler939: Read seizure Data");
					break;
				case Constants.SyncHandlerSequence.WriteStepsHeader:
					Debug.WriteLine("SyncDeviceHandler939: Writing steps header");
					break;
				case Constants.SyncHandlerSequence.WriteDeviceSettings:
					Debug.WriteLine("SyncDeviceHandler939: Writing device settings");
					break;
				case Constants.SyncHandlerSequence.WriteUserSettings:
					Debug.WriteLine("SyncDeviceHandler939: Writing user settings");
					break;
				case Constants.SyncHandlerSequence.WriteExerciseSettings:
					Debug.WriteLine("SyncDeviceHandler939: Writing exercise settings");
					break;
				case Constants.SyncHandlerSequence.WriteCompanySettings:
					Debug.WriteLine("SyncDeviceHandler939: Writing company settings");
					break;
				case Constants.SyncHandlerSequence.WriteSignatureSettings:
					Debug.WriteLine("SyncDeviceHandler939: Writing signature settings");
					break;
				case Constants.SyncHandlerSequence.WriteSeizureSettings:
					Debug.WriteLine("SyncDeviceHandler939: Writing seizure settings");
					break;
				case Constants.SyncHandlerSequence.WriteScreenFlow:
					Debug.WriteLine("SyncDeviceHandler939: Writing screenflow settings");
					break;
				case Constants.SyncHandlerSequence.WriteDeviceSensitivity:
					Debug.WriteLine("SyncDeviceHandler936: Writing device sensitivity settings");
					break;
				case Constants.SyncHandlerSequence.WriteDeviceStatus:
					Debug.WriteLine("SyncDeviceHandler939: Writing device status settings");
					break;
				case Constants.SyncHandlerSequence.WriteScreenDisplay:
					Debug.WriteLine("SyncDeviceHandler939: Writing screen display");
					break;
				case Constants.SyncHandlerSequence.ClearEEProm:
					Debug.WriteLine("SyncDeviceHandler939: Clear eeprom");
					break;
				case Constants.SyncHandlerSequence.WsGetDeviceInfo:
					Debug.WriteLine("SyncDeviceHandler939: WS Request GetDeviceInfo");
					Dictionary<String, object> parameter = new Dictionary<String, object>();
					parameter.Add("serno", "9999999894");
					parameter.Add("fwver", "4.3");
					parameter.Add("mdl", "961");
					parameter.Add("aid", 16781);
					parameter.Add("ddtm", "16-08-12 14:15");
					await WebService.PostData("https://test.savvysherpa.com/DeviceServices/api/Pedometer/GetDeviceInfo", parameter);
					break;
				default:
					Debug.WriteLine("SyncDeviceHandler939: Unable to identify command.");
					break;
			}

			if (this.ProcessQeueue.Count == 0)
			{
				Debug.WriteLine("Queue is already empty. Device will be disconnected.");
				this.Adapter.DisconnectDevice(this.Device);
				this.SyncDone(this, new SyncDoneEventArgs { Status = true });
			}
		}

		public void ReceiveResponse(object sender, CommandResponseEventArgs e)
		{
			Debug.WriteLine("Receiving Response: " + Motion.Mobile.Utilities.Utils.ByteArrayToHexString(e.Data));

			if (this.StartIncrementProgress)
			{
				this.IncrementProgressBar(this, new EventArgs() { });
			}
			switch (this.Command)
			{
				case Constants.SyncHandlerSequence.ReadDeviceStatus:
					Debug.WriteLine("Receving device status");
					this.ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadDeviceSettings:
					Debug.WriteLine("Receving device settings");
					this.ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadUserSettings:
					Debug.WriteLine("Receving user settings");
					this.ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadStepsHeader:
					Debug.WriteLine("Receiving steps header data");
					this.PacketsReceived.Add(e.Data);
					if (Utils.LastPacketReceived(2, e.Data))
					{
						Debug.WriteLine("Last packet received...");
						//Process Data Here...
						this.ProcessCommands();
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
					break;
				case Constants.SyncHandlerSequence.WriteUserSettings:
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
					break;
				default:
					Debug.WriteLine("Invalid response received.");
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

		public void StartSync(Constants.ScanType scanType)
		{
			this.ScanType = scanType;
			this.ProcessQeueue.Clear();
			this.StartIncrementProgress = false;

			if (this.ScanType == Constants.ScanType.ACTIVATION)
			{
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteScreenDisplay);
			}
			else if (this.ScanType == Constants.ScanType.SYNCING)
			{
				Debug.WriteLine("SyncDeviceHandler939: Start syncing-...");
				//this.GetServicesCharacterisitic();
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.EnableFF07);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.EnableFF08);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadModel);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSerial);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadFwVersion);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadBatteryLevel);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadManufacturer);
				//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.GetWsDeviceInfo);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadUserSettings);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadDeviceStatus);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadDeviceSettings);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadStepsHeader);
				this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadCurrentHour);
			}
			this.ProcessCommands();
		}

		public void StartWriteSettings()
		{
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.WriteUserSettings);
			//Other settings to be queued.
		}

		public void SetWebService(IWebServicesWrapper webservice)
		{
			this.WebService = webservice;
		}
	}
}

