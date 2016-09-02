using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Motion.Core.WSHandler;
using Motion.Mobile.Core.BLE;
using Motion.Mobile.Utilities;
using Motion.Core.Data.UserData;


namespace Motion.Core.SyncHandler
{
	public class SyncHandler
	{
		private static readonly object _synclock = new object();
		private static SyncHandler Instance;

		public event EventHandler ScanStarted = delegate { };
		public event EventHandler ScanStopped = delegate { };
		public event EventHandler DeviceConnected = delegate { };
		public event EventHandler DeviceDisconnected = delegate { };
		public event EventHandler ProgressIncrement = delegate { };
		public event EventHandler<SyncProgressCalculatedEventArgs> ProgressCalculated = delegate {};
		public event EventHandler<CharactersDisplayedEventArgs> CharacterssDisplayed = delegate {};
		public event EventHandler<SyncDoneEventArgs> SyncDone = delegate { };
		public event EventHandler<BTStateEventArgs> BTStateChanged = delegate { };

		private IAdapter Adapter;
		private IDevice Device;
		private ISyncDeviceHandler SyncDeviceHandler;
		private IWebServicesWrapper WebService;
		private UserInformation UserInformationInstance;

		private List<Guid> serviceList;
		private bool InScanningMode;
		private bool ProcessingDevice;
		private Constants.ScanType ScanType;



		public SyncHandler()
		{
			this.InScanningMode = false;
			this.ProcessingDevice = false;
			serviceList = new List<Guid>();
			//serviceList.Add(0x180F.UuidFromPartial ());
			//serviceList.Add(new Guid("278B67FE-266B-406C-BD40-25379402B58D"));
			this.SyncDeviceHandler = null;
		}

		public void setAdapter(IAdapter adapter)
		{
			Debug.WriteLine("SyncHandler: Setting adapter");
			this.Adapter = adapter;

			this.Adapter.BluetoothStateUpdated += Adapter_BluetoothStateUpdated;
			this.Adapter.ScanCompleted += Adapter_ScanCompleted;
			this.Adapter.DeviceDiscovered += Adapter_DeviceDiscovered;
			this.Adapter.DeviceConnected += Adapter_DeviceConnected;
			this.Adapter.DeviceDisconnected += Adapter_DeviceDisconnected;
			this.Adapter.DeviceFailedToConnect += Adapter_DeviceFailedToConnect;
		}

		public void SetServiceUUIList(List<Guid> serviceList)
		{
			this.serviceList = serviceList;
		}

		public void SetWebServiceWrapper(IWebServicesWrapper webserviceWrapper)
		{
			this.WebService = webserviceWrapper;
		}

		//GUI to set application info retrieved from ws.
		//AppInfo will be used during data uploading
		public void SetAppInfo()
		{
		}

		//GUI to set user info retrieved from ws.
		//UserInfo will be used during data uploading
		public void SetUserInfo(UserInformation userInfo)
		{
			UserInformationInstance = userInfo;
		}



		public static SyncHandler GetInstance()
		{
			if (Instance == null)
			{
				lock(_synclock)
				{
					if (Instance == null)
					{
						Instance = new SyncHandler();
					}
				}
			}
			return Instance;
		}

		void UpdateProgressBar(object sender, EventArgs args)
		{
			this.ProgressIncrement(this, new EventArgs() { });
		}

		void DoneSyncing(object sender, SyncDoneEventArgs args)
		{
			this.SyncDone(this, new SyncDoneEventArgs() {  });
		}

		public async void StartScan(Constants.ScanType scanType)
		{
			Debug.WriteLine("SyncHandler: StartScan");

			this.ScanType = scanType;
			if (this.Adapter != null && !this.Adapter.IsScanning)
			{
				this.ScanStarted(this, new EventArgs() { });
				await Task.Delay(500);
				this.InScanningMode = true;
				this.Adapter.StartScanningForDevices(this.serviceList);
				this.ScanStarted(this, new EventArgs());
			}
			else {
				Debug.WriteLine("SyncHandler is null");
			}
		}

		public void StopScan()
		{
			this.ScanStopped(this, new EventArgs() { });
			this.InScanningMode = false;
			this.Adapter.StopScanningForDevices();
		}

		public void Connect(IDevice device )
		{
			this.ProcessingDevice = true;
			this.Adapter.ConnectToDevice(device);
		}

		public void Disconnect()
		{
			this.Adapter.DisconnectDevice(Device);

			this.SyncDeviceHandler.IncrementProgressBar -= UpdateProgressBar;
			this.SyncDeviceHandler.SyncDone -= DoneSyncing;
		}

		public void StartWriteSettings()
		{
			this.SyncDeviceHandler.StartWriteSettings();
		}

		//**********EVENTS RECEIVED FROM BLE - Start
		void Adapter_BluetoothStateUpdated(object sender, BluetoothStateEventArgs e)
		{
			Debug.WriteLine("BT state: " + e.BTState);
		}

		void Adapter_ScanCompleted(object sender, EventArgs e)
		{
			Debug.WriteLine("SyncHandler: Scan Completed");
			if (this.InScanningMode)
			{
				this.StartScan(this.ScanType);
			}
		}

		void Adapter_DeviceDiscovered(object sender, DeviceDiscoveredEventArgs e)
		{
			Debug.WriteLine("SyncHandler: Device Discovered.");
			if (e.Device.Name != null)
			{
				if (!this.ProcessingDevice && Utils.isValidDevice(e.Device.Name))
				{
					Debug.WriteLine("Found Valid Device");
					this.StopScan();
					this.Connect(e.Device);
				}
				else if (this.ProcessingDevice)
				{
					Debug.WriteLine("Currently processing valid device.");
				}
			}
		}

		void Adapter_DeviceConnected(object sender, DeviceConnectionEventArgs e)
		{
			Debug.WriteLine("Device Connected");

			this.Device = e.Device;

			//if (this.Device.Name.Replace("PE", "").Replace("FT", "").StartsWith("932"))
			//{
			//	Debug.WriteLine("Instantiating PE932 Handler");
			//	this.syncDeviceHandler = SyncDeviceHandler932.GetInstance();
			//}
			/*else */
			if (this.Device.Name.Replace("PE", "").Replace("FT", "").StartsWith("961"))
			{
				Debug.WriteLine("Instantiating PE/FT961 Handler");
				this.SyncDeviceHandler = SyncDeviceHandler961.GetInstance();
			}
			else if (this.Device.Name.Replace("PE", "").Replace("FT", "").StartsWith("939"))
			{
				Debug.WriteLine("Instantiating PE939 Handler");
				this.SyncDeviceHandler = SyncDeviceHandler939.GetInstance();

			}
			else if (this.Device.Name.StartsWith("H25FE2")) {
				this.SyncDeviceHandler = SyncDeviceHandlerStriiv.GetInstance();
			}

			this.SyncDeviceHandler.IncrementProgressBar += UpdateProgressBar;
			this.SyncDeviceHandler.SyncDone += DoneSyncing;


			this.SyncDeviceHandler.SetAdapter(this.Adapter);
			this.SyncDeviceHandler.SetDevice(this.Device);
			this.SyncDeviceHandler.SetWebService(this.WebService);

			e.Device.ServicesDiscovered += Services_Discovered;
			//this.Adapter.CommandResponse += SyncDeviceHandler.ReceiveResponse;
			e.Device.DiscoverServices();
		}

		void Adapter_DeviceDisconnected(object sender, DeviceConnectionEventArgs e)
		{
			Debug.WriteLine("SyncHandler: Device Disconnected");

			this.DeviceDisconnected(this, new EventArgs() { });

			this.ProcessingDevice = false;
			this.Device = null;
			e.Device.ServicesDiscovered -= Services_Discovered;

			if (this.SyncDeviceHandler != null)
			{
				this.SyncDeviceHandler.CleanUp();
			}

			this.DeviceDisconnected(this, new EventArgs { });

			//this.Adapter.StartScanningForDevices();
		}

		void Adapter_DeviceFailedToConnect(object sender, DeviceConnectionEventArgs e)
		{
		}

		void Adapter_CommandResponse(object sender, CommandResponseEventArgs e)
		{
		}

		void Characteristics_Discovered(object sender, EventArgs e)
		{
		}

		void Services_Discovered(object sender, EventArgs e)
		{
			Debug.WriteLine("SyncHandler: Services Discovered");

			int serviceCount = 0;
			IService lastService = null;
			bool hasChar = false;
			bool foundLastChar = false;
			foreach (var service in this.Device.Services)
			{
				serviceCount++;
				Debug.WriteLine("\tSyncHandler: Services Discovered: " + service.ID.ToString().ToUpper());
				service.CharacteristicsDiscovered += (object s, EventArgs ce) =>
				{
					int charCount = 0;
					foreach (var chr in service.Characteristics)
					{
						charCount++;
						hasChar = true;
						Debug.WriteLine("\t\tSyncHandler: Characteristic Discovered: " + chr.ID.ToString().ToUpper());
						if (foundLastChar)
						{
							Debug.WriteLine("Last na jud ni!");
						}
						if (charCount == service.Characteristics.Count && (lastService != null && lastService.ID == GetService(chr).ID))
						{
							Debug.WriteLine("Found Last Service - iOS: " + service.ID.ToString().ToUpper());
							foundLastChar = true;
							this.DeviceConnected(this, new EventArgs());
							this.startSyncProcess();
						}
					}

				};
				service.DiscoverCharacteristics();

				lastService = service;
				Debug.WriteLine("Last Service: " + lastService.ID);

				//for android implementation- if StarScanning is true, it means it is triggered by android device
				//since android device will always send the characteristicdiscovered because it is always together with the service.
				if (serviceCount == this.Device.Services.Count && hasChar)
				{
					Debug.WriteLine("Found Last Service - Android: " + service.ID.ToString().ToUpper());
					this.DeviceConnected(this, new EventArgs());
					this.startSyncProcess();
				}
			}
		}

		//**********EVENTS RECEIVED FROM BLE - End

		private void startSyncProcess()
		{
			this.SyncDeviceHandler.StartSync(this.ScanType);
		}

		private IService GetService(ICharacteristic chr)
		{
			IService svc = null;
			foreach (var service in this.Device.Services)
			{
				bool found = false;
				foreach (var chars in service.Characteristics)
				{
					if (chars.ID == chr.ID)
					{
						found = true;
						break;
					}
				}
				if (found)
				{
					svc = service;
					break;
				}
			}

			return svc;
		}

		public bool ValidateActivationCode(String enteredCode)
		{
			try
			{
				return this.SyncDeviceHandler.ValidateActivationCode(enteredCode);
			}
			catch (Exception exception)
			{
				Debug.WriteLine(exception.StackTrace.ToString());
				throw new Exception("Error in validating activation code.");
			}
		}

	}
}

