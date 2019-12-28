
using LibGit2Sharp;
using GitRepositoryManager.CredentialManagers;
using System.Threading;
using System;

namespace GitRepositoryManager
{
	/// <summary>
	/// Note readonly once created. Create a new one to change values.
	/// </summary>
	public class Repository
	{
		public class RepoState
		{
			public ICredentialManager CredentialManager;
			public string Url; //Remote repo base
			public string LocalDestination; //Where the repo will be cloned to.
			public string CopyDestination; //Where the required part of the repo will be coppied to.
			public string Branch;
			public string Commit;
		}

		private RepoState _state;
		private volatile bool _inProgress;

		public Repository(ICredentialManager credentials, string url, string localDestination, string copyDestination, string branch = "master", string commit = "")
		{
			_state = new RepoState
			{
				CredentialManager = credentials,
				Url = url,
				LocalDestination = localDestination,
				CopyDestination = copyDestination,
				Branch = branch,
				Commit = commit
			};

			TryUpdate();
		}

		public bool TryUpdate()
		{
			if(!_inProgress)
			{
				_inProgress = true;
				ThreadPool.QueueUserWorkItem(UpdateTask);
				return true;
			}
			else
			{
				return false;
			}
		}

		public void Remove()
		{
			_inProgress = true;
		}

		public float GetProgressValue()
		{
			return 0;
		}

		public string GetProgressMessage()
		{
			return "not implemented";
		}

		/// <summary>
		/// Runs in a thread pool. should clone then checkout the appropriate branch/commit. copy subdirectory into specified repo.
		/// </summary>
		/// <param name="stateInfo"></param>
		private void UpdateTask(object stateInfo)
		{
			//Do as much as possible outside of unity so we dont get constant rebuilds. Only when everything is ready 
			RepoState state = (RepoState)stateInfo;
			CloneOptions options = new CloneOptions()
			{
				CredentialsProvider = (credsUrl, user, supportedCredentials) =>
				{
					state.CredentialManager.GetCredentials(credsUrl, user, supportedCredentials, out var credentials, out string message);
					return credentials;
				},

				IsBare = false, // True will result in a bare clone, false a full clone.
				Checkout = false, // If true, the origin's HEAD will be checked out. This only applies to non-bare repositories.
				BranchName = state.Branch, // The name of the branch to checkout.When unspecified the remote's default branch will be used instead.
				RecurseSubmodules = false, // Recursively clone submodules.
				OnCheckoutProgress = new LibGit2Sharp.Handlers.CheckoutProgressHandler((message, value, total) => { }), // Handler for checkout progress information.
				FetchOptions = new FetchOptions() // Gets or sets the fetch options.
			};

			LibGit2Sharp.Repository.Clone(state.Url, state.LocalDestination, options);

			//Copy step

			//Once completed
			_inProgress = false;
		}
	}
}
