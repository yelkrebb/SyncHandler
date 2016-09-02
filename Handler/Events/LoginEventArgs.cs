using System;
namespace Motion.Core.SyncHandler
{
	public class LoginEventArgs
	{
		public string email { get; set; }
		public string password { get; set; }
		public LoginEventArgs()
		{
		}
	}
}

