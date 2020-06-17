
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

			if (GitProcessHelper.SubmoduleIsValid(state.Destination))
			{
				_progressQueue.Enqueue(new Progress(0, "Found local repository."));
				//TODO: pull submodule using  depth 1
			}
			else
			{
				_progressQueue.Enqueue(new Progress(0, "Initializing clone"));
				//TODO: clone submodule using sparse checkout and depth 1
			}

			//Once completed
			_inProgress = false;
		}


	}
}
