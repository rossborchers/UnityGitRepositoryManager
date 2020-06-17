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

		private string _repositoryCopyRoot;
		private bool _repoWasInProgress;
		public event Action<string, string, string> OnRemovalRequested = delegate { };
		public event Action<List<string>, RepoPanel[]> OnCopyFinished = delegate { };
		public event Action<List<string>> OnDeleteAssetsRequested = delegate { };

		//Note this updates the UI but its not serialized and should not be used for any buisiness logic.
		//This is set when an update attempt occurs, or when we the assembly is reloaded.
		private bool _hasLocalChanges;

		public bool HasLocalChanges()
		{
			string path = RepositoryPath();
			string lastSnapshot = EditorPrefs.GetString(path + "_snapshot");
			string currentSnapshot = SnapshotFolder(path);

			return lastSnapshot != currentSnapshot;
		}

		public void TakeBaselineSnapshot()
		{
			string path = RepositoryPath();
			string newBaseline = SnapshotFolder(path);
			EditorPrefs.SetString(path + "_snapshot", newBaseline);
		}

		// https://stackoverflow.com/questions/3625658/creating-hash-for-folder
		private string SnapshotFolder(string path)
		{
			if(!Directory.Exists(path))
			{
				return "";
			}

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
				return Repository.Get(DependencyInfo.Url, RepositoryPath(), DependencyInfo.SubFolder, DependencyInfo.Branch);
			}
		}

		public RepoPanel(string repositoryCopyRoot, Dependency info)
		{
			DependencyInfo = info;
			_repositoryCopyRoot = repositoryCopyRoot;

			//Note if the hash gets too expensive we may have to cut this (maybe could be async?)
			_hasLocalChanges = HasLocalChanges();
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

			return folders;
		}

		public void OnDrawGUI(int index)
		{
			Rect headerRect = EditorGUILayout.GetControlRect();
			Rect fullRect = new Rect();
			Rect bottomRect = new Rect();

			string foldoutKey = $"RepoPanelFoldout-{DependencyInfo.Url}-{DependencyInfo.Branch}";
			bool expand = EditorGUI.Foldout(headerRect, EditorPrefs.GetBool(foldoutKey, false), "");
			EditorPrefs.SetBool(foldoutKey, expand);

			if (expand)
			{
				bottomRect = EditorGUILayout.GetControlRect(GUILayout.Height(20));

				fullRect = bottomRect;
				fullRect.xMax = Mathf.Max(fullRect.xMax, headerRect.xMax);
				fullRect.yMax = Mathf.Max(fullRect.yMax, headerRect.yMax);
				fullRect.xMin = Mathf.Min(fullRect.xMin, headerRect.xMin);
				fullRect.yMin = Mathf.Min(fullRect.yMin, headerRect.yMin);
			}
			else
			{
				fullRect = headerRect;
			}

			//Overlay to darken every second item
			Rect boxRect = fullRect;
			boxRect.y -= 1.5f;
			boxRect.height += 3;
			boxRect.x -= 5;
			boxRect.width += 10;

			//Header rects

			Rect labelRect = headerRect;
			labelRect.width = headerRect.x + headerRect.width - 53 - 20 - 22 - 5;
			labelRect.height = 18;
			labelRect.x += 15;

			Rect updatingLabelRect = labelRect;
			updatingLabelRect.y += 12;

			Rect progressMessageRect = updatingLabelRect;
			progressMessageRect.y += 16;
			progressMessageRect.width = fullRect.width;

			Rect updateButtonRect = headerRect;
			updateButtonRect.x = headerRect.width - 48;
			updateButtonRect.width = 53;
			updateButtonRect.y += 1;
			updateButtonRect.height = 15;

			Rect removeButtonRect = updateButtonRect;
			removeButtonRect.width = 20;
			removeButtonRect.x = updateButtonRect.x - 20;

			Rect localChangesRect = updateButtonRect;
			localChangesRect.width = 10;
			localChangesRect.x = removeButtonRect.x - localChangesRect.width;
			//Expanded rect

			Rect gitBashRect = bottomRect;
			gitBashRect.x = bottomRect.width - 68;
			gitBashRect.width = 60;
			gitBashRect.y += 1;
			gitBashRect.height = 15;

			//Full Rect
			Repository.Progress lastProgress = _repo.GetLastProgress();

			Rect progressRectOuter = fullRect;
			Rect progressRectInner = fullRect;
			progressRectInner.width = progressRectOuter.width * lastProgress.NormalizedProgress;

			if (index % 2 == 1)
			{
				GUI.Box(boxRect, "");
			}

			//if(_hasLocalChanges)
			{
				GUI.color = Color.yellow;
				GUI.Label(localChangesRect, new GUIContent("*", "Local changes detected. Commit them before proceeding."), EditorStyles.miniBoldLabel);
				GUI.color = Color.white;
			}

			if (_repo.InProgress)
			{
				GUI.Box(progressRectOuter, "");

				GUI.color = !_repo.LastOperationSuccess ? Color.red : Color.green;

				GUI.Box(progressRectInner, "");
				GUI.color = Color.white;

				GUI.Label(updatingLabelRect, ("Updating" ) + GUIUtility.GetLoadingDots(), EditorStyles.miniLabel);

				GUI.Label(progressMessageRect, lastProgress.Message, EditorStyles.miniLabel);
			}
			else if (!_repo.LastOperationSuccess)
			{
				GUI.Label(updatingLabelRect, "Failure", EditorStyles.miniLabel);
				if (GUI.Button(updateButtonRect, "Retry", EditorStyles.miniButton))
				{
					UpdateRepository();
				};
			}
			else
			{
				DrawUpdateButton(updateButtonRect);
			}

			if (!_repo.InProgress && GUI.Button(removeButtonRect, new GUIContent("x", "Remove the repository from this project."), EditorStyles.miniButton))
			{
				if (EditorUtility.DisplayDialog("Remove " + DependencyInfo.Name + "?", "\nThis will remove the repository from the project.\n" +
					((_hasLocalChanges)?"\nAll local changes will be discarded.\n":"") + "\nThis can not be undone.", "Yes", "Cancel"))
				{
					OnRemovalRequested(DependencyInfo.Name, DependencyInfo.Url, RepositoryPath());
				}
			};

			GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
			labelStyle.richText = true;
			GUI.Label(labelRect, DependencyInfo.Name + "  <b><size=9>" + DependencyInfo.Branch + "</size></b>", labelStyle);

			//Draw expanded content
			if (expand)
			{
				//if (GUI.Button(gitBashRect, new GUIContent("Git Bash", "Open git bash to perform more advanced operations"),
				//	EditorStyles.miniButton))
				//{

				//}
			}
		}

		private void DrawUpdateButton(Rect rect)
		{
			if (GUI.Button(rect, new GUIContent("Update", "Pull or clone. Copy into project."), EditorStyles.miniButton))
			{
				if (HasLocalChanges())
				{
					if (EditorUtility.DisplayDialog("Local Changes Detected", DependencyInfo.Name + " has local changes. Updating will permanently delete them. Continue?", "Yes", "No"))
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

		public bool Busy()
		{
			return _repo.InProgress || !_repo.LastOperationSuccess;
		}

		public void UpdateRepository()
		{
			_repo.TryUpdate();
		}
	}
}