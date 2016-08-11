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
<<<<<<< HEAD
		//private EventHandler<CharacteristicReadEventArgs> CharValueUpdated = null;
		private EventHandler<CharacteristicReadEventArgs> NotifyStateUpdated = null;
=======
		private EventHandler<CharacteristicReadEventArgs> CharValueUpdated = null;
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4

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
<<<<<<< HEAD
			Debug.WriteLine("SyncDeviceHandler961: Start syncing-...");
=======
			Debug.WriteLine("SyncDeviceHandler961: Start syncing...");
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
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

<<<<<<< HEAD
		public async void ProcessCommands()
=======
		public void ProcessCommands()
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
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

<<<<<<< HEAD
			ICharacteristic chr = null;

=======
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
			switch (command)
			{
				case Constants.SyncHandlerSequence.EnableFF07:
					Debug.WriteLine("SyncDeviceHandler961: Enabling FF07 characteristic");
					ff07Char = GetServicesCharacterisitic(Constants.CharacteristicsUUID._FF07);
<<<<<<< HEAD
					if (ff07Char == null) Debug.WriteLine("FF07 is NULL");
					ff07Char.StartUpdates();
=======
					ff07Char.StartUpdates();
					Task.Delay(500);
					ProcessCommands();
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
					break;
				case Constants.SyncHandlerSequence.EnableFF08:
					Debug.WriteLine("SyncDeviceHandler961: Enabling FF08 characteristic");
					ff08Char = GetServicesCharacterisitic(Constants.CharacteristicsUUID._FF08);
					ff08Char.StartUpdates();
<<<<<<< HEAD
=======
					Task.Delay(500);
					ProcessCommands();
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
					break;
				case Constants.SyncHandlerSequence.ReadModel:
					Debug.WriteLine("SyncDeviceHandler961: Reading model from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A24);
<<<<<<< HEAD
					chr = await readChar.ReadAsync();
					Debug.WriteLine("Model: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
=======
					readChar.ReadAsync();
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
					break;
				case Constants.SyncHandlerSequence.ReadSerial:
					Debug.WriteLine("SyncDeviceHandler961: Reading serial from characterisitic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A25);
<<<<<<< HEAD
					chr = await readChar.ReadAsync();
					Debug.WriteLine("Serial: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
=======
					readChar.ReadAsync();
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
					break;
				case Constants.SyncHandlerSequence.ReadFwVersion:
					Debug.WriteLine("SyncDeviceHandler961: Reading fw version from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A26);
<<<<<<< HEAD
					chr = await readChar.ReadAsync();
					Debug.WriteLine("Firmware Version: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
=======
					readChar.ReadAsync();
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
					break;
				case Constants.SyncHandlerSequence.ReadManufacturer:
					Debug.WriteLine("SyncDeviceHandler961: Reading manufacturer from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A29);
<<<<<<< HEAD
					chr = await readChar.ReadAsync();
					Debug.WriteLine("Manufacturer: " + System.Text.Encoding.UTF8.GetString(chr.Value, 0, chr.Value.Length));
					ProcessCommands();
=======
					readChar.ReadAsync();
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
					break;
				case Constants.SyncHandlerSequence.ReadBatteryLevel:
					Debug.WriteLine("SyncDeviceHandler961: Reading battery level from characteristic.");
					readChar = GetServicesCharacterisitic(Constants.CharacteristicsUUID._2A19);
<<<<<<< HEAD
					chr = await readChar.ReadAsync();
					Debug.WriteLine("Battery Level: " + (int) chr.Value[0]);
					ProcessCommands();
=======
					readChar.ReadAsync();
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
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
<<<<<<< HEAD
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
=======
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
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
							}
						}
						break;
					}
				}
			}

			return characterisitic;
		}

<<<<<<< HEAD
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
=======
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

>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
			this.ProcessCommands();
		}

		public void DescriptorWrite(object sender, EventArgs e)
		{
			Debug.WriteLine("Success writing descriptor.");
			ProcessCommands();
		}
<<<<<<< HEAD


=======
>>>>>>> 74ff625341a48fc0c980b4a1f11023c0845b13c4
	}
}

