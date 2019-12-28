
using GitRepositoryManager.CredentialManagers;
using LibGit2Sharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;

namespace GitRepositoryManager
{
	public class RepositoryTester
	{
		private class CallbackData
		{
			public Action<bool, string> Callback;
			public Tuple<bool, string> Data;
		}

		private ConcurrentQueue<CallbackData> _callbacks = new ConcurrentQueue<CallbackData>();

		public bool Testing
		{
			get;
			private set;
		}

		private class TestState
		{
			public string Url;
			public ICredentialManager CredentialManager;
			public Action<bool, string> OnComplete;
		}

		public void Test(string url, ICredentialManager credentialManager, Action<bool, string> onComplete)
		{
			if(Testing)
			{
				throw new Exception("[RepositoryTester] Already busy testing.");
			}

			Testing = true;
			ThreadPool.QueueUserWorkItem(TestRepositoryValid, new TestState { Url = url, CredentialManager = credentialManager, OnComplete = onComplete });
		}

		private void TestRepositoryValid(object state)
		{
			TestState testState = (TestState)state;

			bool credentialSuccess = false;
			string credentialMessage = "Credential flow has yet to complete. Did the test callback execute prematurely?";
			try
			{
				// Credential error message handling got  a bit complicated as libgit2sharp eats exceptions and reports "this remote has never connected" for any problem.
				// See https://github.com/libgit2/libgit2sharp/issues/1504		
				IEnumerable<Reference> references = LibGit2Sharp.Repository.ListRemoteReferences(testState.Url, new LibGit2Sharp.Handlers.CredentialsHandler((url, user, supportedCredentialTypes) =>
				{
					if(testState.CredentialManager == null)
					{
						credentialSuccess = false;
						credentialMessage = "Manager passed into repository tester is null!";
						return new DefaultCredentials();
					}
					try
					{
						credentialSuccess = testState.CredentialManager.GetCredentials(url, user, supportedCredentialTypes, out var creds, out var message);

						if (credentialSuccess)
						{
							credentialMessage = "Succeeded getting credentials";
						}
						else
						{
							credentialMessage =  message;
						}
						return creds;
					}
					catch(Exception e)
					{
						credentialSuccess = false;
						credentialMessage = e.Message;
						return new DefaultCredentials();
					}
				}));

				if(!credentialSuccess)
				{
					_callbacks.Enqueue(new CallbackData() { Callback = testState.OnComplete, Data = new Tuple<bool, string>(false, "Failed getting credentials: " + credentialMessage) });
				}
				else
				{
					bool validRemote = references.Count() > 0;
					if (validRemote)
					{
						_callbacks.Enqueue(new CallbackData() { Callback = testState.OnComplete, Data = new Tuple<bool, string>(true, "Success. The url points to a valid git repository.") });
					}
					else
					{
						_callbacks.Enqueue(new CallbackData() { Callback = testState.OnComplete, Data = new Tuple<bool, string>(false, "Failed to connect to url\n" + testState.Url) });
					}
				}
			}
			catch (Exception e)
			{
				if (!credentialSuccess)
				{
					_callbacks.Enqueue(new CallbackData() { Callback = testState.OnComplete, Data = new Tuple<bool, string>(false, "Failed getting credentials: " + credentialMessage) });
				}
				else
				{
					_callbacks.Enqueue(new CallbackData() { Callback = testState.OnComplete, Data = new Tuple<bool, string>(false, "Failed to connect to url\n" + testState.Url + "\n" + e.Message) });
				}			
			}
		}

		//Caller is responsible for checking the repository test has finished.
		public void Update()
		{
			if (_callbacks.Count > 0)
			{
				if(!_callbacks.TryDequeue(out var data))
				{
					throw new Exception("[RepositoryHelper] Failed to dequeue but data exists.");
				}

				data.Callback(data.Data.Item1, data.Data.Item2);
				Testing = false;
			}
		}
	}
}
