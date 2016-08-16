using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Motion.Core.WSHandler;
using Motion.Mobile.Core.BLE;

namespace Motion.Core.SyncHandler
{
	public class SyncDeviceHandler961 : ISyncDeviceHandler
	{
		private static readonly object _synclock = new object();
		private static SyncDeviceHandler961 Instance;

		private IAdapter Adapter;
		private IDevice Device;
		private IWebServicesWrapper WebService;

		private ICharacteristic Ff07Char;
		private ICharacteristic Ff08Char;
		private ICharacteristic ReadChar;

		private Queue<Constants.SyncHandlerSequence> ProcessQeueue = new Queue<Constants.SyncHandlerSequence>();
		private Constants.SyncHandlerSequence Command;
		private EventHandler<CharacteristicReadEventArgs> NotifyStateUpdated = null;

		private List<byte[]> packetsReceived = new List<byte[]>();

		private SyncDeviceHandler961()
		{
			WebService = new WebService();
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
			this.Device = null;
		}

		public void StartSync()
		{
			Debug.WriteLine("SyncDeviceHandler961: Start syncing-...");
			//this.GetServicesCharacterisitic();
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.EnableFF07);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.EnableFF08);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadModel);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSerial);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadFwVersion);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadBatteryLevel);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadManufacturer);
			//this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.GetWsDeviceInfo);
			this.ProcessQeueue.Enqueue(Constants.SyncHandlerSequence.ReadStepsHeader);
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
					Debug.WriteLine("SyncDeviceHandler961: Enabling FF07 characteristic");
					Ff07Char = GetServicesCharacterisitic(Constants.CharacteristicsUUID._FF07);
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
					Ff08Char = GetServicesCharacterisitic(Constants.CharacteristicsUUID._FF08);
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
					ReadChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A24);
					chr = await ReadChar.ReadAsync();
					Debug.WriteLine("Model: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadSerial:
					Debug.WriteLine("SyncDeviceHandler961: Reading serial from characterisitic.");
					ReadChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A25);
					chr = await ReadChar.ReadAsync();
					Debug.WriteLine("Serial: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadFwVersion:
					Debug.WriteLine("SyncDeviceHandler961: Reading fw version from characteristic.");
					ReadChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A26);
					chr = await ReadChar.ReadAsync();
					Debug.WriteLine("Firmware Version: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadManufacturer:
					Debug.WriteLine("SyncDeviceHandler961: Reading manufacturer from characteristic.");
					ReadChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A29);
					chr = await ReadChar.ReadAsync();
					Debug.WriteLine("Manufacturer: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadBatteryLevel:
					Debug.WriteLine("SyncDeviceHandler961: Reading battery level from characteristic.");
					ReadChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A19);
					chr = await ReadChar.ReadAsync();
					Debug.WriteLine("Battery Level: " + (int) chr.Value[0]);
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadStepsHeader:
					Debug.WriteLine("SyncDeviceHandler961: Read Steps Header-");
					var data = new byte[] { 0x1B, 0x22};
					this.Adapter.CommandResponse += ReceiveResponse;
					this.Adapter.SendCommand(Ff07Char, data);
					break;
				case Constants.SyncHandlerSequence.GetWsDeviceInfo:
					Debug.WriteLine("SyncDeviceHandler961: WS Request GetDeviceInfo");
					Dictionary<String, object> parameter = new Dictionary<String, object>();
					parameter.Add("serno", "9999999894");
					parameter.Add("fwver", "4.3");
					parameter.Add("mdl", "961");
					parameter.Add("aid", 16781);
					parameter.Add("ddtm", "16-08-12 14:15");
					await WebService.PostData("https://test.savvysherpa.com/DeviceServices/api/Pedometer/GetDeviceInfo", parameter);
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

		public ICharacteristic GetServicesCharacterisitic(Constants.CharacteristicsUUID uuid)
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

		public void ReceiveResponse(object sender, CommandResponseEventArgs e)
		{
			Debug.WriteLine("Receiving Response: " + Utils.ByteArrayToHexString(e.Data));

			switch (this.Command)
			{
				case Constants.SyncHandlerSequence.ReadStepsHeader:
					Debug.WriteLine("Receiving steps header data: " + Utils.ByteArrayToHexString(e.Data));
					this.packetsReceived.Add(e.Data);
					if (Utils.LastPacketReceived(2, e.Data))
					{
						Debug.WriteLine("Last packet received...");
						//Process Data Here...
						this.ProcessCommands();
					}
					break;
				default:
					break;
			}
		}

		public void DescriptorWrite(object sender, EventArgs e)
		{
			Debug.WriteLine("Success writing descriptor.");
			ProcessCommands();
		}


	}
}

