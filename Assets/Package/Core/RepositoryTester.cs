
using System;
using System.Collections.Concurrent;
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
			public string Branch;
			public string SubFolder;
			public Action<bool, string> OnComplete;
		}

		public void Test(string url, string branch, string subFolder, Action<bool, string> onComplete)
		{
			if(Testing)
			{
				throw new Exception("[RepositoryTester] Already busy testing.");
			}

			Testing = true;
			ThreadPool.QueueUserWorkItem(TestRepositoryValid, new TestState { Url = url, Branch = branch, SubFolder = subFolder, OnComplete = onComplete });
		}

		private void TestRepositoryValid(object state)
		{
			//TODO: check subfolder is a valid URL
			TestState testState = (TestState)state;

			try
			{
				string message = string.Empty;
				if (GitProcessHelper.CheckRemoteExists(testState.Url, testState.Branch,
					(success, msg) => { message = msg; }))
				{
					_callbacks.Enqueue(new CallbackData()
					{
						Callback = testState.OnComplete,
						Data = new Tuple<bool, string>(true, "Success. The url points to a valid git repository.")
					});
				}
				else
				{
					_callbacks.Enqueue(new CallbackData()
					{
						Callback = testState.OnComplete,
						Data = new Tuple<bool, string>(false, $"Failed to connect to url\n{testState.Url} {message}")
					});
				}
			}
			catch (Exception e)
			{
				_callbacks.Enqueue(new CallbackData()
				{
					Callback = testState.OnComplete,
					Data = new Tuple<bool, string>(false, $"Failed to connect to url\n{testState.Url} {e.Message}")
				});
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
