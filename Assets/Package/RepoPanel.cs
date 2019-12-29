using GitRepositoryManager.CredentialManagers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GitRepositoryManager
{
	public class RepoPanel
	{
		public Dependency DependencyInfo
		{
			get;
			private set;
		}

		private ICredentialManager _credentialManager;
		private string _repositoryCopyRoot;
		private bool _repoWasInProgress;
		public event Action<string, string, string, string> OnRemovalRequested = delegate { };

		public bool HasLocalChanges()
		{
			string path = CopyPath(_repositoryCopyRoot);
			string lastSnapshot = EditorPrefs.GetString(path + "_snapshot");
			string currentSnapshot = SnapshotCopyFolder(path);

			return lastSnapshot != currentSnapshot;
		}

		public void TakeBaselineSnapshot()
		{
			string path = CopyPath(_repositoryCopyRoot);
			string newBaseline = SnapshotCopyFolder(path);
			EditorPrefs.SetString(path + "_snapshot", newBaseline);
		}

		// https://stackoverflow.com/questions/3625658/creating-hash-for-folder
		private string SnapshotCopyFolder(string path)
		{
			// assuming you want to include nested folders
			var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
								 .OrderBy(p => p).ToList();

			MD5 md5 = MD5.Create();

			for (int i = 0; i < files.Count; i++)
			{
				string file = files[i];

				// hash path
				string relativePath = file.Substring(path.Length + 1);
				byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath.ToLower());
				md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

				// hash contents
				byte[] contentBytes = File.ReadAllBytes(file);
				if (i == files.Count - 1)
					md5.TransformFinalBlock(contentBytes, 0, contentBytes.Length);
				else
					md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
			}

			return BitConverter.ToString(md5.Hash).Replace("-", "").ToLower();
		}

		private Repository _repo
		{
			get
			{
				//Link to repository task needs to survive editor reloading in case job is in progress. We do this by never storing references. always calling get. This will lazy init if the repo does not exist else reuse the repo.
				return Repository.Get(_credentialManager, DependencyInfo.Url, RepositoryPath(), CopyPath(_repositoryCopyRoot), DependencyInfo.SubFolder, DependencyInfo.Branch);
			}
		}

		public RepoPanel(string repositoryCopyRoot, Dependency info, ICredentialManager credentials)
		{
			DependencyInfo = info;
			_credentialManager = credentials;
			_repositoryCopyRoot = repositoryCopyRoot;
		}

		public bool Update()
		{
			if (_repo.InProgress || !_repo.LastOperationSuccess)
			{
				_repoWasInProgress = true;
				return true;
			}

			//Clear changes on the frame after repo finished updating.
			if (_repoWasInProgress)
			{
				_repoWasInProgress = false;
				return true;
			}

			return false;
		}

		public string RepositoryPath()
		{
			string[] split = DependencyInfo.Url.Split(new string[] { "://" }, StringSplitOptions.RemoveEmptyEntries);
			string folders = DependencyInfo.Url;
			if (split.Length > 1)
			{
				folders = split[1];
				folders = folders.Replace(".", "/");
				folders = folders.Replace(":", "");
			}
			else
			{
				Debug.LogWarning("Failed to parse:" + DependencyInfo.Url + ". Undefined behaviour may result when trying to update repository");
			}

			if (folders.IndexOfAny(Path.GetInvalidPathChars()) != -1 || string.IsNullOrEmpty(folders))
			{
				Debug.LogError("Path is invalid" + folders + ". Undefined behaviour may result when trying to update repository");
			}

			//We keep repositories on seperate branches seperate as we want to be able to copy back working changes with the knowledge that we are on the correct checkout.
			folders = Path.Combine(folders, DependencyInfo.Branch);

			return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnityGitRepositoryCache"), folders);
		}

		public string CopyPath(string root)
		{
			return Path.Combine(root, DependencyInfo.Name);
		}

		public void OnDrawGUI(int index)
		{
			Rect rect;

			if (_repo.InProgress || !_repo.LastOperationSuccess)
			{
				rect = EditorGUILayout.GetControlRect(GUILayout.Height(50));
			}
			else
			{
				rect = EditorGUILayout.GetControlRect();
			}

			Rect boxRect = rect;
			boxRect.y -= 1.5f;
			boxRect.height += 3;
			boxRect.x -= 5;
			boxRect.width += 10;

			Rect labelRect = rect;
			labelRect.width = rect.x + rect.width - 50 - 15 - 5;

			Rect updatingLabelRect = labelRect;
			updatingLabelRect.y += 12;

			Rect progressMessageRect = updatingLabelRect;
			progressMessageRect.y += 16;
			progressMessageRect.width = rect.width;

			Rect buttonRect = rect;
			buttonRect.x = rect.width - 48;
			buttonRect.width = 50;
			buttonRect.y += 1;
			buttonRect.height = 15;

			Rect removeButtonRect = buttonRect;
			removeButtonRect.width = 15;
			removeButtonRect.x = buttonRect.x - 15;

			Repository.Progress lastProgress = _repo.GetLastProgress();

			Rect progressRectOuter = rect;
			Rect progressRectInner = rect;
			progressRectInner.width = progressRectOuter.width * lastProgress.NormalizedProgress;

			if (index % 2 == 1)
			{
				GUI.Box(boxRect, "");
			}

			if ((_repo.InProgress || !_repo.LastOperationSuccess))
			{
				GUI.Box(progressRectOuter, "");

				GUI.color = _repo.CancellationPending || !_repo.LastOperationSuccess ? Color.red : Color.green;

				GUI.Box(progressRectInner, "");
				GUI.color = Color.white;

				if (_repo.LastOperationSuccess)
				{
					GUI.Label(updatingLabelRect, (_repo.CancellationPending ? "Cancelling" : "Updating") + GUIUtility.GetLoadingDots(), EditorStyles.miniLabel);
				}
				else
				{
					GUI.Label(updatingLabelRect, "Failure", EditorStyles.miniLabel);
				}

				GUI.Label(progressMessageRect, lastProgress.Message, EditorStyles.miniLabel);


				if (_repo.LastOperationSuccess)
				{
					//TODO: Cancel not fully implemented due to complexities in libgit2sharp. Hiding for now.
					/*if (!_repo.CancellationPending && GUI.Button(buttonRect, "Cancel", EditorStyles.miniButton))
					{
						_repo.CancelUpdate();
					};*/
				}
				else
				{
					if (GUI.Button(buttonRect, "Retry", EditorStyles.miniButton))
					{
						UpdateRepository();
					};
				}
			}
			else
			{
				if (GUI.Button(buttonRect, "Update", EditorStyles.miniButton))
				{
					if(HasLocalChanges())
					{
						if (EditorUtility.DisplayDialog("Local Changes Detected", DependencyInfo.Name + " has local changes. Updating will permenantly delete them. Continue?", "Yes", "No"))
						{
							UpdateRepository();
						}
					}
					else
					{
						UpdateRepository();
					}
				};
			}

			if (!_repo.InProgress && GUI.Button(removeButtonRect, "x", EditorStyles.miniButton))
			{
				if (EditorUtility.DisplayDialog("Remove " + DependencyInfo.Name + "?", "\nThis will remove the repository from the project and Dependencies.json\n\nThis can not be undone", "Yes", "Cancel"))
				{
					OnRemovalRequested(DependencyInfo.Name, DependencyInfo.Url, RepositoryPath(), CopyPath(_repositoryCopyRoot));
				}
			};

			GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
			labelStyle.richText = true;
			GUI.Label(labelRect, DependencyInfo.Name + "  <b><size=9>" + /*(String.IsNullOrEmpty(DependencyInfo.Branch) ? (DependencyInfo.Tag + " (tag)") :*/ DependencyInfo.Branch/*)*/ + "</size></b>", labelStyle);
		}

		public bool Busy()
		{
			return _repo.InProgress || !_repo.LastOperationSuccess;
		}

		public void UpdateRepository()
		{
			_repo.TryUpdate();
		}

		public void CancelUpdateRepository()
		{
			_repo.CancelUpdate();
		}

		public List<string> CopyRepository()
		{
			return _repo.Copy();
		}
	}
}