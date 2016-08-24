using System;
namespace Motion.Core.SyncHandler
{
	public class SyncDoneEventArgs : EventArgs
	{
		public bool Status;
		public SyncDoneEventArgs() : base()
		{
		}
	}
}

