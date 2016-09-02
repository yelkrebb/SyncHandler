using System;
using System.Collections.Generic;
using System.Diagnostics;
using Motion.Core.WSHandler;
using Motion.Mobile.Core.BLE;
using Motion.Mobile.Utilities;
using Motion.Core.Data.UserData;

namespace Motion.Core.SyncHandler
{
	public class SyncDeviceHandlerStriiv : ISyncDeviceHandler
	{
		private static readonly object _synclock = new object();
		private static SyncDeviceHandlerStriiv Instance;

		private IAdapter Adapter;
		private IDevice Device;
		private IWebServicesWrapper WebService;

		private ICharacteristic Char9A0A;
		private ICharacteristic CharFE23;

		private Queue<Constants.StriivSyncHandlerSequence> ProcessQeueue = new Queue<Constants.StriivSyncHandlerSequence>();
		private Constants.StriivSyncHandlerSequence Command;
		private EventHandler<CharacteristicReadEventArgs> NotifyStateUpdated = null;

		private byte[] CommandRequest;
		private List<byte[]> PacketsReceived = new List<byte[]>();

		public event EventHandler IncrementProgressBar;
		public event EventHandler<SyncDoneEventArgs> SyncDone;

		private SyncDeviceHandlerStriiv()
		{
		}

		public static SyncDeviceHandlerStriiv GetInstance()
		{
			if (Instance == null)
			{
				lock (_synclock)
				{
					if (Instance == null)
					{
						Instance = new SyncDeviceHandlerStriiv();
					}
				}
			}

			return Instance;
		}

		public void CleanUp()
		{
			this.ProcessQeueue.Clear();
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

		public void ProcessCommands()
		{
			Debug.WriteLine("SyncDeviceHandlerStriiv: ProcessCommands");

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
				case Constants.StriivSyncHandlerSequence.EnableFE23:
					Debug.WriteLine("SyncDeviceHandlerSTRIIV: Enabling FE23 characteristic");
					CharFE23 = GetServicesCharacteristic(Constants.CharacteristicsUUID._FE23);
					if (CharFE23 == null)
					{
						Debug.WriteLine("FE23 is NULL. Disconnecting device.");
						this.Adapter.DisconnectDevice(Device);
					}
					else {
						CharFE23.StartUpdates();
					}
					break;
				case Constants.StriivSyncHandlerSequence.Ack:
					Debug.WriteLine("Ack response");
					CommandRequest = new byte[] { 0x08 };
					this.Adapter.SendCommand(Char9A0A, CommandRequest);
					break;
				case Constants.StriivSyncHandlerSequence.DeviceInfo:
					Debug.WriteLine("Read Device Info");
					CommandRequest = new byte[] { 0x06 };
					this.Adapter.SendCommand(Char9A0A, CommandRequest);
					break;
				case Constants.StriivSyncHandlerSequence.RegisterRead:
					Debug.WriteLine("Register Read Command");
					CommandRequest = new byte[] { 0x00 };
					this.Adapter.SendCommand(Char9A0A, CommandRequest);
					break;
				case Constants.StriivSyncHandlerSequence.RegisterWrite:
					Debug.WriteLine("Register Write Time");
					//CommandRequest = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x30, 0x1E, 0x02, 0x16, 0x07, 0x07, 0xE0 };
					CommandRequest = new byte[19];
					CommandRequest[0] = 0x01;
					CommandRequest[1] = 0x00;
					CommandRequest[2] = 0x00;
					CommandRequest[3] = 0x01;//00
					CommandRequest[4] = 0x1E;//01
					CommandRequest[5] = 0x0F;//02
					CommandRequest[6] = 0x0B;//03
					CommandRequest[7] = 0x18;//04
					CommandRequest[8] = 0x08;//05
					CommandRequest[9] = 0x7E;//06
					CommandRequest[10] = 0x00;//07
					CommandRequest[11] = 0x00;//08
					CommandRequest[12] = 0x00;//09
					CommandRequest[13] = 0x00;//10
					CommandRequest[14] = 0x00;//11
					CommandRequest[15] = 0x00;//12
					CommandRequest[16] = 0x00;//13
					CommandRequest[17] = 0x00;//14
					CommandRequest[18] = 0x00;//15

					this.Adapter.SendCommand(Char9A0A, CommandRequest);
					break;
				case Constants.StriivSyncHandlerSequence.FileLoadList:
					Debug.WriteLine("Reading File Load List");
					CommandRequest = new byte[] { 0x02, 0x01 };
					this.Adapter.SendCommand(Char9A0A, CommandRequest);
					break;
				default:
					Debug.WriteLine("Invalid command request...");
					break;
			}
		}

		public void ReceiveResponse(object sender, CommandResponseEventArgs e)
		{
			Debug.WriteLine("Receiving Response: " + Motion.Mobile.Utilities.Utils.ByteArrayToHexString(e.Data));

			if (e.Data[0] == 0x88)
			{
				Debug.WriteLine("Receiving ack. Ignoring...");
			}
			else if (e.Data[0] == 0x87)
			{
				Debug.WriteLine("Receiving activity streaming. Ignoring...");
			}
			else {

				switch (this.Command)
				{
					case Constants.StriivSyncHandlerSequence.DeviceInfo:
						Debug.WriteLine("Receiving Device Info");
						this.PacketsReceived.Add(e.Data);
						if (this.PacketsReceived.Count >= 5)
						{
							this.ParseDeviceInfo(e.Data);
							this.ProcessCommands();
						}
						break;
					case Constants.StriivSyncHandlerSequence.RegisterRead:
						Debug.WriteLine("Receiving register read");
						this.ProcessCommands();
						break;
					case Constants.StriivSyncHandlerSequence.RegisterWrite:
						Debug.WriteLine("Receiving register write time response");
						this.ProcessCommands();
						break;
					case Constants.StriivSyncHandlerSequence.FileLoadList:
						Debug.WriteLine("Receiving File Load List");
						//this.ProcessCommands();
						break;
					default:
						break;
				}
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
			Debug.WriteLine("SyncDeviceHandlerStriiv: Start syncing....");

			Char9A0A = GetServicesCharacteristic(Constants.CharacteristicsUUID._9A0A);
			this.ProcessQeueue.Enqueue(Constants.StriivSyncHandlerSequence.EnableFE23);
			//this.ProcessQeueue.Enqueue(Constants.StriivSyncHandlerSequence.DeviceInfo);
			//this.ProcessQeueue.Enqueue(Constants.StriivSyncHandlerSequence.RegisterWrite);
			//this.ProcessQeueue.Enqueue(Constants.StriivSyncHandlerSequence.RegisterRead);
			this.ProcessQeueue.Enqueue(Constants.StriivSyncHandlerSequence.FileLoadList);
			//this.ProcessQeueue.Enqueue(Constants.StriivSyncHandlerSequence.DeviceInfo);

			this.ProcessCommands();
		}

		public void NotifyStateUpdateDone(object sender, CharacteristicReadEventArgs e)
		{
			Debug.WriteLine("SyncHandlerDeviceStriiv: Notification State Updated: " + e.Characteristic.ID);
			switch (Command)
			{
				case Constants.StriivSyncHandlerSequence.EnableFE23:
					Debug.WriteLine("Done enabling FE23");
					this.Adapter.CommandResponse += ReceiveResponse;
					break;
				default:
					Debug.WriteLine("Unknown response!");
					break;

			}
			this.ProcessCommands();
		}

		public void StartWriteSettings()
		{
			throw new NotImplementedException();
		}

		public void SetWebService(IWebServicesWrapper webservice)
		{
			this.WebService = webservice;
		}

		private void ParseDeviceInfo(byte[] bytes)
		{
			List<byte> cleanData = new List<byte>();
			byte streamingNumber = 0x00;
			byte[] messageLength = new byte[3];

			//Clean packets received - start
			for (int i = 0; i < this.PacketsReceived.Count; i++)
			{
				byte[] indexBytes = this.PacketsReceived[i];

				//first bit of first byte
				int bit = ((indexBytes[0] & 0x80) > 1) ? 1 : 0;

				//first packet
				if (bit == 1)
				{
					streamingNumber = indexBytes[1];
					messageLength[0] = indexBytes[2];
					messageLength[1] = indexBytes[3];
					messageLength[2] = indexBytes[4];
					//Array.C
				}
				//succedding packets
				else {
				}
			}
			//Clean packets received - end
		}

		public bool ValidateActivationCode(string enteredCode)
		{
			throw new NotImplementedException();
		}

		public void SetUserInfo(UserInformation userInfo)
		{
			throw new NotImplementedException();
		}

	}
}

