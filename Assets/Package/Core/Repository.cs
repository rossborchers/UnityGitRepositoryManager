
using System.Threading;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace GitRepositoryManager
{
	/// <summary>
	/// Note readonly once created. Create a new one to change values.
	/// </summary>
	public class Repository
	{
		private static List<Repository> _repos = new List<Repository>();
		public static Repository Get(string url, string destination, string subFolder, string branch = "master", string tag = "")
		{
			foreach(Repository repo in _repos)
			{
				if(repo._state.Url == url &&
					repo._state.Destination == destination &&
					repo._state.SubFolder == subFolder)
				{
					if (repo._state.Branch != branch || repo._state.Tag != tag)
					{
						throw new Exception("[Repository] A repository exists that points to a different branch or tag, but the copy destination is the same!");
					}

					return repo;
				}
			}

			Repository newRepo = new Repository(url, destination, subFolder, branch, tag);
			_repos.Add(newRepo);
			return newRepo;
		}

		public static void Remove(string url, string destination)
		{
			for(int i = _repos.Count-1; i >=0 ; i--)
			{
				Repository repo = _repos[i];
				if (repo._state.Url == url &&
					repo._state.Destination == destination)
				{
					_repos[i].TryRemoveCopy();
					_repos.RemoveAt(i);
				}
			}
		}
		public static int TotalInitialized
		{
			get
			{
				return _repos.Count;
			}
		}

		public class RepoState
		{
			public string Url; //Remote repo base
			public string Destination; //Where the repo will be cloned to..
			public string SubFolder;
			public string Branch;
			public string Tag;
		}

		private RepoState _state;
		private volatile bool _inProgress;
		private volatile bool _cancellationPending;
		private volatile bool _lastOperationSuccess = true;

		public struct Progress
		{
			public Progress(float normalizedProgress, string message)
			{
				NormalizedProgress = normalizedProgress;
				Message = message;
			}

			public float NormalizedProgress;
			public string Message;
		}

		private ConcurrentQueue<Progress> _progressQueue = new ConcurrentQueue<Progress>();

		public Repository(string url, string destination, string subFolder, string branch = "master", string tag = "")
		{
			_state = new RepoState
			{
				Url = url,
				Destination = destination,
				SubFolder = subFolder,
				Branch = branch,
				Tag = tag
			};
		}

		public bool TryUpdate()
		{
			if(!_inProgress)
			{
				_inProgress = true;
				_lastOperationSuccess = true;
				ThreadPool.QueueUserWorkItem(UpdateTask, _state);
				return true;
			}
			else
			{
				return false;
			}
		}

		public bool TryRemoveCopy()
		{
			if (!Directory.Exists(_state.Destination))
			{
				return false;
			}

			Directory.Delete(_state.Destination, true);
			return true;
		}

		public void CancelUpdate()
		{
			if(_inProgress) _cancellationPending = true;
			else
			{
				_cancellationPending = false;
				_lastOperationSuccess = true;
			}
		}

		public bool InProgress
		{
			get
			{
				return _inProgress;
			}
		}

		public bool LastOperationSuccess
		{
			get
			{
				return _lastOperationSuccess;
			}
		}

		public bool CancellationPending
		{
			get
			{
				return _cancellationPending;
			}
		}

		public Progress GetLastProgress()
		{
			Progress currentProgress;
			if(_progressQueue.Count > 0)
			{
				if(_progressQueue.Count > 1)
				{
					_progressQueue.TryDequeue(out currentProgress);
				}
				else
				{
					_progressQueue.TryPeek(out currentProgress);
				}
			}
			else
			{
				currentProgress = new Progress(0, "Update Pending");
			}

			return currentProgress;
		}

		/// <summary>
		/// Runs in a thread pool. Should clone then checkout the appropriate branch/commit. copy subdirectory into specified repo.
		/// </summary>
		/// <param name="stateInfo"></param>
		private void UpdateTask(object stateInfo)
		{
			//Do as much as possible outside of unity so we dont get constant rebuilds. Only when everything is ready
			RepoState state = (RepoState)stateInfo;

			if(state == null)
			{
				_lastOperationSuccess = false;
				_progressQueue.Enqueue(new Progress(0, "Repository state info is null"));
				return;
			}
			/*
			if (state.CredentialManager == null)
			{
				_lastOperationSuccess = false;
				_progressQueue.Enqueue(new Progress(0, "Credentials manager is null"));
				return;
			}

			FetchOptions fetchOptions = new FetchOptions()
			{
				/*TagFetchMode = TagFetchMode.All,
				OnTransferProgress = new LibGit2Sharp.Handlers.TransferProgressHandler((progress) =>
				{
					_progressQueue.Enqueue(new Progress(((float)progress.ReceivedObjects) / progress.TotalObjects, "Fetching " + progress.ReceivedObjects + "/" + progress.TotalObjects + "(" + progress.ReceivedBytes + " bytes )"));

					return _cancellationPending;
				}),*//*
				CredentialsProvider = (credsUrl, user, supportedCredentials) =>
				{
					state.CredentialManager.GetCredentials(credsUrl, user, supportedCredentials, out var credentials, out string message);
					return credentials;
				}
			};

			if (LibGit2Sharp.Repository.IsValid(state.LocalDestination))
			{
				_progressQueue.Enqueue(new Progress(0, "Found local repository."));

				//Repo exists we are doing a pull
				using (var repo = new LibGit2Sharp.Repository(state.LocalDestination))
				{
					//_progressQueue.Enqueue(new Progress(0, "Nuking local changes. Checking out " + state.Branch));

					//Branch branch = repo.Branches[state.Branch];
					//Commands.Checkout(repo, branch, new CheckoutOptions() { CheckoutModifiers = CheckoutModifiers.Force, CheckoutNotifyFlags = CheckoutNotifyFlags.None});

					// Credential information to fetch
					PullOptions options = new PullOptions
					{
						FetchOptions = fetchOptions
					};

					// User information to create a merge commit. Should not happen as we force checkout before pulling.
					var signature = new LibGit2Sharp.Signature(
						new Identity("RepositoryManager", "repositorymanager@mergemail.com"), DateTimeOffset.Now);

					try
					{
						_progressQueue.Enqueue(new Progress(0, "Pulling from " + state.Url));
						Commands.Pull(repo, signature, options);
						_progressQueue.Enqueue(new Progress(1, "Complete"));
						_lastOperationSuccess = true;
					}
					catch (Exception e)
					{
						_progressQueue.Enqueue(new Progress(0, "Pull failed: " + e.Message));
						_lastOperationSuccess = false;
					}

					/*try
					{
						var remote = repo.Network.Remotes["origin"];
						var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);

						_progressQueue.Enqueue(new Progress(0, "Fetching from " + remote.Name));

						Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, "");

						_progressQueue.Enqueue(new Progress(1, "Complete"));

						try
						{
							Branch branch = repo.Branches["origin/" + state.Branch];
							var signature = new Signature(new Identity("RepositoryManager", "Repositorymanager@Mergemail.com"), DateTimeOffset.Now);
							repo.Merge(branch, signature);

							_lastOperationSuccess = true;
						}
						catch (Exception e)
						{
							_progressQueue.Enqueue(new Progress(0, "Merge failed: " + e.Message));
							_lastOperationSuccess = false;
						}
					}
					catch (Exception e)
					{
						_progressQueue.Enqueue(new Progress(0, "Fetch failed: " + e.Message));
						_lastOperationSuccess = false;
					}*//*
				}
			}
			else
			{
				_progressQueue.Enqueue(new Progress(0, "Initializing clone"));

				//Repo does not exist. Clone it.
				CloneOptions options = new CloneOptions()
				{
					CredentialsProvider = (credsUrl, user, supportedCredentials) =>
					{
						if (!state.CredentialManager.GetCredentials(credsUrl, user, supportedCredentials, out var credentials, out string message))
						{
							throw new Exception("Failed to get credentials: " + message);
						}
						return credentials;
					},

					IsBare = false, // True will result in a bare clone, false a full clone.
					Checkout = true, // If true, the origin's HEAD will be checked out. This only applies to non-bare repositories.
					BranchName = state.Branch, // The name of the branch to checkout. When unspecified the remote's default branch will be used instead.
					RecurseSubmodules = false, // Recursively clone submodules.
					OnCheckoutProgress = new LibGit2Sharp.Handlers.CheckoutProgressHandler((message, value, total) =>
					{
						_progressQueue.Enqueue(new Progress(Math.Max(Math.Min(((float)value) / total, 1), 0), message));
					}), // Handler for checkout progress information.
					FetchOptions = fetchOptions
				};

				try
				{
					_progressQueue.Enqueue(new Progress(0, "Cloning " + state.Url));
					LibGit2Sharp.Repository.Clone(state.Url, state.LocalDestination, options);
					_progressQueue.Enqueue(new Progress(1, "Complete"));
					_lastOperationSuccess = true;
				}
				catch(Exception e)
				{
					_progressQueue.Enqueue(new Progress(0, "Clone failed: " + e.Message));
					_lastOperationSuccess = false;
				}
			}

			if(LastOperationSuccess)
			{
				_progressQueue.Enqueue(new Progress(1, "Downloading LFS files"));
				try
				{
					InstallAndPullLFS(state.LocalDestination);
				}
				catch(Exception e)
				{
					_progressQueue.Enqueue(new Progress(0, "LFS Pull failed: " + e.Message));
					_lastOperationSuccess = false;
				}
			}

			//Once completed
			_inProgress = false;
			_cancellationPending = false;
		}

		public void OpenRepositoryDestination()
		{
			Process.Start(new ProcessStartInfo()
			{
				FileName = _state.LocalDestination,
				UseShellExecute = true,
				Verb = "open"
			});
			//Leave the process running. User should close it manually.
		}

		public void InstallAndPullLFS(string path)
		{
			//Install lfs
			ProcessStartInfo installStartInfo = new ProcessStartInfo
			{
				FileName = "git-lfs",
				Arguments = "install",
				WorkingDirectory = path,
				UseShellExecute = false,
				WindowStyle = ProcessWindowStyle.Hidden,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			Process installProcess = new Process
			{
				StartInfo = installStartInfo
			};

			installProcess.Start();
			installProcess.WaitForExit();

			//Now pull lfs
			ProcessStartInfo pullStartInfo = new ProcessStartInfo
			{
				FileName = "git-lfs",
				Arguments = "pull",
				WorkingDirectory = path,
				UseShellExecute = false,
				WindowStyle = ProcessWindowStyle.Hidden,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			Process pullProcess = new Process
			{
				StartInfo = pullStartInfo
			};

			pullProcess.Start();
			pullProcess.WaitForExit();*/
		}
	}
}
