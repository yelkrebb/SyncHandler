using System;
using Motion.Core.Data.AppData;
namespace Motion.Core.SyncHandler
{
	public class AppEventArgs : EventArgs
	{
		public ApplicationInfo appInfo { get; set; }
		public AppEventArgs()
		{
		}
	}
}

