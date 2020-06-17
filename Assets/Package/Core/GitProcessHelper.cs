using System;
using System.Diagnostics;
using System.Text;

namespace GitRepositoryManager
{
    //This blocks on the caller thread. Should be run through a thread pool.
    public static class GitProcessHelper
    {
        public static string LastError
        {
            get;
            private set;
        }

        public static bool CheckRemoteExists(string url, string branch)
        {
            if (RunCommand(null, $"git ls-remote {url} {branch}", out string output))
            {
                if(output.Contains("refs/heads"))
                {
                    return true;
                }
                else
                {
                    LastError = "No repository or branch found";
                    return false;
                }
            }
            else
            {
                LastError = output;
                return false;
            }
        }

        private static bool RunCommand(string directory, string command, out string output)
        {
            try
            {
                ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", $"/c {command}");

                procStartInfo.RedirectStandardError = true;
                procStartInfo.RedirectStandardInput = true;
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = true;

                if (!string.IsNullOrEmpty(directory))
                {
                    procStartInfo.WorkingDirectory = directory;
                }

                Process proc = new Process
                {
                    StartInfo = procStartInfo
                };
                proc.Start();

                StringBuilder sb = new StringBuilder();
                proc.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    sb.AppendLine(e.Data);
                };
                proc.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    sb.AppendLine(e.Data);
                };

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
                output = sb.ToString();
                return true;
            }
            catch (Exception objException)
            {
                output = $"Error in command: {command}, {objException.Message}";
                return false;
            }
        }
    }
}