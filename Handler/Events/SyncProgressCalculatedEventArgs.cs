using System;
namespace Motion.Core.SyncHandler
{
	public class SyncProgressCalculatedEventArgs : EventArgs
	{
		public int MaxPackets;
		public SyncProgressCalculatedEventArgs() : base()
		{}
	}
}

