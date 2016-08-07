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
		private EventHandler<CharacteristicReadEventArgs> CharValueUpdated = null;

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
			Debug.WriteLine("SyncDeviceHandler961: Start syncing...");
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

		public void ProcessCommands()
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

			switch (command)
			{
				case Constants.SyncHandlerSequence.EnableFF07:
					Debug.WriteLine("SyncDeviceHandler961: Enabling FF07 characteristic");
					ff07Char = GetServicesCharacterisitic(Constants.CharacteristicsUUID._FF07);
					ff07Char.StartUpdates();
					Task.Delay(500);
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.EnableFF08:
					Debug.WriteLine("SyncDeviceHandler961: Enabling FF08 characteristic");
					ff08Char = GetServicesCharacterisitic(Constants.CharacteristicsUUID._FF08);
					ff08Char.StartUpdates();
					Task.Delay(500);
					ProcessCommands();
					break;
				case Constants.SyncHandlerSequence.ReadModel:
					Debug.WriteLine("SyncDeviceHandler961: Reading model from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A24);
					readChar.ReadAsync();
					break;
				case Constants.SyncHandlerSequence.ReadSerial:
					Debug.WriteLine("SyncDeviceHandler961: Reading serial from characterisitic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A25);
					readChar.ReadAsync();
					break;
				case Constants.SyncHandlerSequence.ReadFwVersion:
					Debug.WriteLine("SyncDeviceHandler961: Reading fw version from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A26);
					readChar.ReadAsync();
					break;
				case Constants.SyncHandlerSequence.ReadManufacturer:
					Debug.WriteLine("SyncDeviceHandler961: Reading manufacturer from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A29);
					readChar.ReadAsync();
					break;
				case Constants.SyncHandlerSequence.ReadBatteryLevel:
					Debug.WriteLine("SyncDeviceHandler961: Reading battery level from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A19);
					readChar.ReadAsync();
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
				Debug.WriteLine("Service: " + service.ID);
				foreach (var chr in service.Characteristics)
				{
					Debug.WriteLine("Characteristic: " + chr.ID);
					if (chr.Uuid.ToString().ToUpper().Contains(uuid.ToString().Replace("_", "")))
					{
						Debug.WriteLine("Characteristic Found!");
						characterisitic = chr;
						if (characterisitic.CanRead)
						{
							if (CharValueUpdated == null)
							{
								Debug.WriteLine("Subscribing valueupdated!");
								CharValueUpdated = new EventHandler<CharacteristicReadEventArgs>(GetValueFromChar);
								characterisitic.ValueUpdated += CharValueUpdated;
							}
						}
						break;
					}
				}
			}

			return characterisitic;
		}

		public void GetValueFromChar(object sender, CharacteristicReadEventArgs e)
		{
			switch (command)
			{
				case Constants.SyncHandlerSequence.ReadModel:
					Debug.WriteLine("Model: " + System.Text.Encoding.UTF8.GetString(e.Characteristic.Value, 0, e.Characteristic.Value.Length));
					break;
				case Constants.SyncHandlerSequence.ReadSerial:
					Debug.WriteLine("Serial: " + System.Text.Encoding.UTF8.GetString(e.Characteristic.Value, 0, e.Characteristic.Value.Length));
					break;
				case Constants.SyncHandlerSequence.ReadFwVersion:
					Debug.WriteLine("Fw Version: " + System.Text.Encoding.UTF8.GetString(e.Characteristic.Value, 0, e.Characteristic.Value.Length));
					break;
				case Constants.SyncHandlerSequence.ReadManufacturer:
					Debug.WriteLine("Manufacturer: " + System.Text.Encoding.UTF8.GetString(e.Characteristic.Value, 0, e.Characteristic.Value.Length));
					break;
				case Constants.SyncHandlerSequence.ReadBatteryLevel:
					Debug.WriteLine("Battery: " + (int) e.Characteristic.Value[0]);
					break;
				default:
					Debug.WriteLine("SyncDeviceHandler961: Unable to identify command.");
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

