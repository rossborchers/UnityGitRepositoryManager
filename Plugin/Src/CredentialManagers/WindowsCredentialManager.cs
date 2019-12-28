using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace GitRepositoryManager.CredentialManagers
{
	public class WindowsCredentialManager : ICredentialManager
	{
		public bool GetCredentials(string url, string user, SupportedCredentialTypes supportedCredentials, out Credentials credentials, out string message)
		{
			try
			{
                credentials = GetCredentialsFromTerminal(url);
				message = "[WindowsCredentialManager] Recieved credentials";
				return true;
			}
			catch(Exception e)
			{
				credentials = new DefaultCredentials();
				//This is a hack to get the exception information out. since Lib2GitSharp keeps eating it!
				message = "[WindowsCredentialManager] Exception getting credentials: " + e.Message;
				return false;
			}
			
		}

        //https://stackoverflow.com/questions/50010941/libgit2sharp-and-authentication-ui
        public UsernamePasswordCredentials GetCredentialsFromTerminal(string url)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git.exe",
                Arguments = "credential fill",
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();

            // Write query to stdin. 
            // For stdin to work we need to send \n instead of WriteLine
            // We need to send empty line at the end
            var uri = new Uri(url);
            process.StandardInput.NewLine = "\n";
            process.StandardInput.WriteLine($"protocol={uri.Scheme}");
            process.StandardInput.WriteLine($"host={uri.Host}");
            process.StandardInput.WriteLine($"path={uri.AbsolutePath}");
            process.StandardInput.WriteLine();

            // Get user/pass from stdout
            string username = null;
            string password = null;
            string line;
            while ((line = process.StandardOutput.ReadLine()) != null)
            {
                string[] details = line.Split('=');
                if (details[0] == "username")
                {
                    username = details[1];
                }
                else if (details[0] == "password")
                {
                    password = details[1];
                }
            }

            return new UsernamePasswordCredentials()
            {
                Username = username,
                Password = password
            };
        }
    }
}
