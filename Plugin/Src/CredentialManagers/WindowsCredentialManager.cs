using LibGit2Sharp;
using Microsoft.Alm.Authentication;
using System;
using System.Collections.Generic;
using System.Text;

namespace GitRepositoryManager.CredentialManagers
{
	public class WindowsCredentialManager : ICredentialManager
	{
		SecretStore secrets;
		BasicAuthentication auth;

		public WindowsCredentialManager()
		{
			secrets = new SecretStore("gitrepositorymanager");
			auth = new BasicAuthentication(secrets);
		}

		public bool GetCredentials(string url, string user, SupportedCredentialTypes supportedCredentials, out Credentials credentials, out string message)
		{
			try
			{
				Credential creds = auth.AcquireCredentials(new TargetUri(url)).Result;
				if(creds == null)
				{
					credentials = new DefaultCredentials();
					message = "[WindowsCredentialManager] credentials returned from auth are null";
					return false;
				}

				credentials = new UsernamePasswordCredentials() { Username = creds.Username, Password = creds.Password };
				message = "[WindowsCredentialManager] Recieved credentials";
				return false;
			}
			catch(Exception e)
			{
				credentials = new DefaultCredentials();
				//This is a hack to get the exception information out. since Lib2GitSharp keeps eating it!
				message = "[WindowsCredentialManager] Exception getting credentials: " + e.Message;
				return false;
			}
			
		}
	}
}
