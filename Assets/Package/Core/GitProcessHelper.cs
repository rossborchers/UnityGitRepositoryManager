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

        public static bool SubmoduleIsValid(string directory)
        {
            //TODO:
            return true;
        }

        public static bool AddSubmodule(string rootDirectory, string directory, string url, string branch)
        {
            //TODO: as seen here https://stackoverflow.com/questions/30129920/git-submodule-without-extra-weight/38895397#38895397. So git pulls do not affect submodules.
            //what does this mean for pull through sourcetree?
            //return RunCommand(rootDirectory,"git config -f .gitmodules submodule.<name>.shallow true")
            return RunCommand(rootDirectory, $"git submodule add -b {branch} --depth 1 {url} {directory}", out string output);
        }

        public static bool InitSubmodule(string rootDirectory, string directory, string url, string branch)
        {
            //TODO: sparse checkout must go under: .git/modules/<mymodule>/info/ as per https://stackoverflow.com/questions/6238590/set-git-submodule-to-shallow-clone-sparse-checkout
            return RunCommand(rootDirectory, $"git sparse-checkout init --cone", out string output);
        }

        public static void OpenRepositoryInExplorer(string directory)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = directory,
                UseShellExecute = true,
                Verb = "open"
            });
            //Leave the process running. User should close it manually.
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