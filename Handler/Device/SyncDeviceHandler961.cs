using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Motion.Mobile.Core.BLE;

namespace Motion.Core.SyncHandler
{
	public class SyncDeviceHandler961 : ISyncDeviceHandler
	{
		private static readonly object _synclock = new object();
		private static SyncDeviceHandler961 Instance;

		private IAdapter Adapter;
		private IDevice Device;

		private ICharacteristic ff07Char;
		private ICharacteristic ff08Char;
		private ICharacteristic readChar;

		private Queue<Constants.SyncHandlerSequence> processQeueue = new Queue<Constants.SyncHandlerSequence>();
		private Constants.SyncHandlerSequence command;
		//private EventHandler<CharacteristicReadEventArgs> CharValueUpdated = null;
		private EventHandler<CharacteristicReadEventArgs> NotifyStateUpdated = null;

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
			throw new NotImplementedException();
		}

		public void StartSync()
		{
			Debug.WriteLine("SyncDeviceHandler961: Start syncing-...");
			//this.GetServicesCharacterisitic();
			this.processQeueue.Enqueue(Constants.SyncHandlerSequence.EnableFF07);
			this.processQeueue.Enqueue(Constants.SyncHandlerSequence.EnableFF08);
			this.processQeueue.Enqueue(Constants.SyncHandlerSequence.ReadModel);
			this.processQeueue.Enqueue(Constants.SyncHandlerSequence.ReadSerial);
			this.processQeueue.Enqueue(Constants.SyncHandlerSequence.ReadFwVersion);
			this.processQeueue.Enqueue(Constants.SyncHandlerSequence.ReadBatteryLevel);
			this.processQeueue.Enqueue(Constants.SyncHandlerSequence.ReadManufacturer);
			this.ProcessCommands();
		}

		public async void ProcessCommands()
		{
			Debug.WriteLine("SyncHandler: ProcessCommands");

			if (this.processQeueue.Count > 0)
			{
				command = this.processQeueue.Dequeue();
			}
			else {
				Debug.WriteLine("No more commands to be processed!");
				return;
			}

			ICharacteristic chr = null;

			switch (command)
			{
				case Constants.SyncHandlerSequence.EnableFF07:
					Debug.WriteLine("SyncDeviceHandler961: Enabling FF07 characteristic");
					ff07Char = GetServicesCharacterisitic(Constants.CharacteristicsUUID._FF07);
					if (ff07Char == null) Debug.WriteLine("FF07 is NULL");
					ff07Char.StartUpdates();
					break;
				case Constants.SyncHandlerSequence.EnableFF08:
					Debug.WriteLine("SyncDeviceHandler961: Enabling FF08 characteristic");
					ff08Char = GetServicesCharacterisitic(Constants.CharacteristicsUUID._FF08);
					ff08Char.StartUpdates();
					break;
				case Constants.SyncHandlerSequence.ReadModel:
					Debug.WriteLine("SyncDeviceHandler961: Reading model from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A24);
					chr = await readChar.ReadAsync();
					Debug.WriteLine("Model: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadSerial:
					Debug.WriteLine("SyncDeviceHandler961: Reading serial from characterisitic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A25);
					chr = await readChar.ReadAsync();
					Debug.WriteLine("Serial: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadFwVersion:
					Debug.WriteLine("SyncDeviceHandler961: Reading fw version from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A26);
					chr = await readChar.ReadAsync();
					Debug.WriteLine("Firmware Version: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadManufacturer:
					Debug.WriteLine("SyncDeviceHandler961: Reading manufacturer from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A29);
					chr = await readChar.ReadAsync();
					Debug.WriteLine("Manufacturer: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadBatteryLevel:
					Debug.WriteLine("SyncDeviceHandler961: Reading battery level from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A19);
					chr = await readChar.ReadAsync();
					Debug.WriteLine("Battery Level: " + (int) chr.Value[0]);
					ProcessCommands();
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
				//Debug.WriteLine("Service: " + service.ID);
				foreach (var chr in service.Characteristics)
				{
					//Debug.WriteLine("Characteristic: " + chr.ID);
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
			switch (command)
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

		public void DescriptorWrite(object sender, EventArgs e)
		{
			Debug.WriteLine("Success writing descriptor.");
			ProcessCommands();
		}


	}
}

