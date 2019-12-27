using LibGit2Sharp;
using Microsoft.Alm.Authentication;
using System;
using System.Collections.Generic;
using System.Text;

namespace PackageManager.CredentialManagers
{
	public class WindowsCredentialManager : ICredentialManager
	{
		SecretStore secrets;
		BasicAuthentication auth;

		public WindowsCredentialManager()
		{
			secrets = new SecretStore("git");
			auth = new BasicAuthentication(secrets);
		}

		public Credentials GetCredentials(string url, string user, SupportedCredentialTypes supportedCredentials)
		{
			Credential creds = auth.GetCredentials(new TargetUri(url));
			return new UsernamePasswordCredentials() { Username = creds.Username, Password = creds.Password };
		}
	}
}
