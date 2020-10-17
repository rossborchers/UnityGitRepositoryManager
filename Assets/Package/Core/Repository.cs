using System;
using System.IO;
using UnityEngine;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace GitRepositoryManager
{
	/// <summary>
	/// Logical component of a repository, 1-1 relationship with GUIRepopositoryPanel
	/// Read only once created. Create a new one to chaTnge values.
	/// </summary>
	public class Repository
	{
		private static List<Repository> _repos = new List<Repository>();
		public static Repository Get(string url, string branch, string rootFolder, string repositoryFolder, string directoryInRepository)
		{
			foreach(Repository repo in _repos)
			{
				if(repo._state.Url == url &&
					repo._state.RootFolder ==  rootFolder &&
					repo._state.RepositoryFolder == repositoryFolder)
				{
					if (repo._state.Branch != branch)
					{
						throw new Exception($"[Repository] A repository exists that points to a different branch, but its folder is the same!" +
						                    $"\n{repo._state.Url}/{repo._state.Branch} vs {url}/{branch}");
					}
					return repo;
				}
			}

			Repository newRepo = new Repository(url, branch, rootFolder, repositoryFolder, directoryInRepository);
			_repos.Add(newRepo);
			return newRepo;
		}

		public static void Remove(string url, string rootFolder)
		{
			for(int i = _repos.Count-1; i >=0 ; i--)
			{
				Repository repo = _repos[i];
				if (repo._state.Url == url &&
					repo._state.RepositoryFolder == rootFolder)
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
			public string Branch; // The branch the repo will be on
			public string RootFolder; //The root of the git repo. Absolute path.
			public string RepositoryFolder; //The folder that the repository will be initialized in. Relative from RootFolder.
			public string DirectoryInRepository; //The folder in the repository that will be checked out sparsely. Relative from RootFolder.
		}

		private readonly RepoState _state;

		//shared between thread pool process and main thread
		private volatile bool _inProgress;
		private volatile bool _refreshPending = false;

		public bool RefreshPending
		{
			get => _refreshPending;
			set => _refreshPending = value;
		}

		public string AbsolutePath
		{
			get
			{
				Debug.Assert(_state != null, "State is null when trying to get AbsolutePath in repository");
				return $"{_state.RootFolder}/{_state.RepositoryFolder}";
			}
		}

		public struct Progress
		{
			public Progress(float normalizedProgress, string message, bool error)
			{
				NormalizedProgress = normalizedProgress;
				Message = message;
				Error = error;
			}

			public float NormalizedProgress;
			public string Message;
			public bool Error;
		}

		private ConcurrentQueue<Progress> _progressQueue = new ConcurrentQueue<Progress>();

		public Repository(string url, string branch, string rootFolder, string repositoryFolder, string directoryInRepository)
		{
			_state = new RepoState
			{
				Url = url,
				Branch = branch,

				RootFolder = rootFolder,
				RepositoryFolder = repositoryFolder,
				DirectoryInRepository = directoryInRepository
			};
		}

		public bool TryUpdate()
		{
			if(!_inProgress)
			{
				_inProgress = true;
				ThreadPool.QueueUserWorkItem(UpdateTask, _state);
				return true;
			}
			else
			{
				return false;
			}
		}

		public void OpenTerminal()
		{
			GitProcessHelper.OpenRepositoryInTerminal(_state.RootFolder, _state.RepositoryFolder);
		}

		public void PushChanges()
		{
			ThreadPool.QueueUserWorkItem(PushTask, _state);
		}

		public bool TryRemoveCopy()
		{

			if (!Directory.Exists(AbsolutePath))
			{
				return false;
			}

			// remove read only attribute on all files so we can delete them (this is primarily for the .git folders files, as git sets readonly)
			var files = Directory.GetFiles(AbsolutePath, "*.*", SearchOption.AllDirectories).OrderBy(p => p).ToList();
			foreach (string filePath in files)
			{
				File.SetAttributes(filePath, FileAttributes.Normal);
			}

			Directory.Delete(AbsolutePath, true);
			return true;
		}

		public bool InProgress => _inProgress;

		public bool LastOperationSuccess
		{
			get
			{
				if (_progressQueue.Count <= 0) return true;
				_progressQueue.TryPeek(out Progress progress);
				return !progress.Error;
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
				currentProgress = new Progress(0, "Update Pending", false);
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
				_progressQueue.Enqueue(new Progress(0, "Repository state info is null",true));
				return;
			}

			if (GitProcessHelper.RepositoryIsValid(state.RepositoryFolder, OnProgress))
			{
				GitProcessHelper.UpdateRepository(state.RootFolder,state.RepositoryFolder, state.DirectoryInRepository, state.Url, state.Branch, OnProgress);
			}
			else
			{
				GitProcessHelper.AddRepository(state.RootFolder, state.RepositoryFolder, state.DirectoryInRepository, state.Url, state.Branch, OnProgress);
			}

			//Once completed
			if (_progressQueue.Count > 0)
			{
				//Get the latest progress
				if (!_progressQueue.ToArray()[_progressQueue.Count-1].Error)
				{
					_refreshPending = true;
				}
			}

			_inProgress = false;

			void OnProgress(bool success, string message)
			{
				_progressQueue.Enqueue(new Progress(0, message, !success));
			}
		}

		/// <summary>
		/// Runs in a thread pool. Should push based on settings of push window. copy subdirectory into specified repo.
		/// </summary>
		/// <param name="stateInfo"></param>
		private void PushTask(object stateInfo)
		{
			//Do as much as possible outside of unity so we dont get constant rebuilds. Only when everything is ready
			RepoState state = (RepoState)stateInfo;

			if(state == null)
			{
				_progressQueue.Enqueue(new Progress(0, "Repository state info is null",true));
				return;
			}

			if (GitProcessHelper.RepositoryIsValid(state.RepositoryFolder, OnProgress))
			{
				GitProcessHelper.PushRepository(state.RootFolder,state.RepositoryFolder, state.DirectoryInRepository, state.Url, state.Branch, OnProgress);
			}

			//Once completed
			if (_progressQueue.Count > 0)
			{
				//Get the latest progress
				if (!_progressQueue.ToArray()[_progressQueue.Count-1].Error)
				{
					_refreshPending = true;
				}
			}

			_inProgress = false;

			void OnProgress(bool success, string message)
			{
				_progressQueue.Enqueue(new Progress(0, message, !success));
			}
		}


		//TODO: redo for whole folder
		private void SetIgnore(string parentRoot, string relativeFolderToIgnore, bool ignore)
		{
			//This is a pretty ok attempt at automating adding to gitignore. If people go in and add to the ignore manually this may not pick that up but that's fine.
			string ignoreFile = $"{parentRoot}/.gitignore";
			string ignoreString = $"{relativeFolderToIgnore}/*";
			if (!File.Exists(ignoreFile))
			{
				Debug.LogWarning($"[RepositoryManager] Can not find {ignoreFile}. Not ignoring repository");
				return;
			}

			List<string> lines = new List<string>(File.ReadAllLines(ignoreFile));
			if (!ignore)
			{
				//If we are not ignoring we look through all the lines to remove the folder if it was previously ignored
				for (int i = lines.Count - 1; i >= 0; i++)
				{
					if (lines[i].Contains(ignoreString))
					{
						lines.RemoveAt(i);
					}
				}
			}
			else
			{
				//Else we add the ignore string to the end of the file.
				lines.Add(ignoreString);
			}
		}
	}
}
