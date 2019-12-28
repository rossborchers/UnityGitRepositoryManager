using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System;
using System.Collections.Generic;
using System.Text;

namespace GitRepositoryManager.CredentialManagers
{
	public interface ICredentialManager
	{
		bool GetCredentials(string url, string user, SupportedCredentialTypes supportedCredentials, out Credentials credentials, out string message);
	}
}
