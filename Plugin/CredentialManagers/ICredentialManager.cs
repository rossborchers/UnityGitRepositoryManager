using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System;
using System.Collections.Generic;
using System.Text;

namespace PackageManager.CredentialManagers
{
	public interface ICredentialManager
	{
		Credentials GetCredentials(string url, string user, SupportedCredentialTypes supportedCredentials);
	}
}
