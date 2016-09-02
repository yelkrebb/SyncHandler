using System;
namespace Motion.Core.SyncHandler
{
	public class StatusEventArgs : EventArgs
	{
		public bool isEqual { get; set; }
		public StatusEventArgs()
		{
		}
	}
}

