using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEditor.PackageManager;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace GitRepositoryManager
{
	public class GUIRepositoryPanel
	{
		public Dependency DependencyInfo
		{
			get;
			private set;
		}

		private bool _repoWasInProgress;
		public event Action<string, string, string> OnRemovalRequested = delegate { };
		public event Action<Dependency, string> OnEditRequested = delegate { };
		public event Action<GUIRepositoryPanel[]> OnRefreshRequested = delegate { };

		//Note this updates the UI but its not serialized and should not be used for any buisiness logic.
		//This is set when an update attempt occurs, or when we the assembly is reloaded.
		private bool _hasLocalChanges;

		private Texture2D _editIcon;
		private Texture2D _removeIcon;

		public string RootFolder()
		{
			string fullPath = Application.dataPath;
			return fullPath.Replace("/Assets", "");
		}

		public string RelativeRepositoryPath()
		{
			return $"Assets/Repositories/{DependencyInfo.Name}";
		}

		public string RelativeRepositoryFolderPath()
		{
			return $"{RelativeRepositoryPath()}/{DependencyInfo.SubFolder}";
		}

		public bool HasLocalChanges(bool useCached = false)
		{
			if (useCached)
			{
				return _hasLocalChanges;
			}

			string path = RelativeRepositoryPath();
			string lastSnapshot = EditorPrefs.GetString(path + "_snapshot");
			string currentSnapshot = SnapshotFolder(path);
			_hasLocalChanges = lastSnapshot != currentSnapshot;
			return _hasLocalChanges;
		}

		public void TakeBaselineSnapshot()
		{
			//TODO: think about refactoring this to using git
			string path = RelativeRepositoryPath();
			string newBaseline = SnapshotFolder(path);
			EditorPrefs.SetString(path + "_snapshot", newBaseline);
		}

		// https://stackoverflow.com/questions/3625658/creating-hash-for-folder
		private string SnapshotFolder(string path)
		{
			//UnityEngine.Debug.Log("Performing snapshot for: " + path);
			if(!Directory.Exists(path))
			{
				return "";
			}

			// assuming you want to include nested folders
			var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
								 .OrderBy(p => p).ToList();

			// Get all meta files to remove them from the file list (since meta files change regularly on import)
			var metaFiles = Directory.GetFiles(path, "*.meta", SearchOption.AllDirectories)
				.OrderBy(p => p).ToList();

			metaFiles.ForEach((meta) => { files.Remove(meta);});

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
				return Repository.Get(DependencyInfo.Url,  DependencyInfo.Branch, RootFolder(),RelativeRepositoryPath(), RelativeRepositoryFolderPath());
			}
		}

		public GUIRepositoryPanel(Dependency info, Texture2D editIcon, Texture2D removeIcon)
		{
			DependencyInfo = info;

			//Note if the hash gets too expensive we may have to cut this (maybe could be async?)
			_hasLocalChanges = HasLocalChanges();

			_editIcon = editIcon;
			_removeIcon = removeIcon;
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

		public void OnDrawGUI(int index)
		{
			Rect headerRect = EditorGUILayout.GetControlRect();
			Rect fullRect = new Rect();
			Rect bottomRect = new Rect();

			fullRect = headerRect;

			//Overlay to darken every second item
			Rect boxRect = fullRect;
			boxRect.y -= 1.5f;
			boxRect.height += 3;
			boxRect.x -= 5;
			boxRect.width += 10;

			//Header rects

			Rect labelRect = headerRect;
			labelRect.width = headerRect.x + headerRect.width - 53 - 20 - 20 - 22 - 5;
			labelRect.height = 18;
			labelRect.x += 15;

			Rect updateButtonRect = headerRect;
			updateButtonRect.x = headerRect.width - 48;
			updateButtonRect.width = 53;
			updateButtonRect.y += 1;
			updateButtonRect.height = 15;

			Rect editButtonRect = updateButtonRect;
			editButtonRect.width = 20;
			editButtonRect.x -= 20;

			Rect removeButtonRect = editButtonRect;
			removeButtonRect.x -= 20;

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

			if(_hasLocalChanges)
			{
				GUI.color = Color.yellow;
				GUI.Label(localChangesRect, new GUIContent("*", "Local changes detected. Commit them before proceeding."), EditorStyles.miniBoldLabel);
				GUI.color = Color.white;
			}

			if (_repo.InProgress)
			{
				GUI.Box(progressRectOuter, "");

				GUI.color = lastProgress.Error ? Color.red : Color.green;

				GUI.Box(progressRectInner, "");
				GUI.color = Color.white;

				//GUI.Label(updatingLabelRect, ( ) + , EditorStyles.miniLabel);
				//GUI.Label(progressMessageRect, , EditorStyles.miniLabel);
			}
			else if (lastProgress.Error)
			{
				GUIStyle failureStyle = new GUIStyle(EditorStyles.label);
				failureStyle.richText = true;
				failureStyle.alignment = TextAnchor.MiddleRight;
				GUI.Label(labelRect, new GUIContent("<b><color=red>Failure</color></b>", lastProgress.Message), failureStyle);
				if (GUI.Button(updateButtonRect, "Retry", EditorStyles.miniButton))
				{
					UpdateRepository();
				};
			}
			else
			{
				DrawUpdateButton(updateButtonRect);
			}

			GUIStyle iconButtonStyle = new GUIStyle( EditorStyles.miniButton);
			iconButtonStyle.padding = new RectOffset(3,3,3,3);
			if (!_repo.InProgress && GUI.Button(removeButtonRect, new GUIContent(_removeIcon, "Remove the repository from this project."), iconButtonStyle))
			{
				if (EditorUtility.DisplayDialog("Remove " + DependencyInfo.Name + "?", "\nThis will remove the repository from the project.\n" +
					((_hasLocalChanges)?"\nAll local changes will be discarded.\n":"") + "\nThis can not be undone.", "Yes", "Cancel"))
				{
					OnRemovalRequested(DependencyInfo.Name, DependencyInfo.Url, RelativeRepositoryPath());
				}
			};
			if (!_repo.InProgress && GUI.Button(editButtonRect, new GUIContent(_editIcon, "Remove the repository from this project."), iconButtonStyle))
			{
				OnEditRequested(DependencyInfo, RelativeRepositoryPath());
			};

			GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
			labelStyle.richText = true;

			GUI.Label(labelRect, DependencyInfo.Name + "  <b><size=9>" + DependencyInfo.Branch + "</size></b>" +
			                     (_repo.InProgress ? $" <i><size=9>{lastProgress.Message}{GUIUtility.GetLoadingDots()}</size></i>" : ""), labelStyle);

			if (_repo.RefreshPending)
			{
				OnRefreshRequested(new[]{this});
				_repo.RefreshPending = false;
			}

			//Draw expanded content
			//if (expand)
			//{
				//if (GUI.Button(gitBashRect, new GUIContent("Git Bash", "Open git bash to perform more advanced operations"),
				//	EditorStyles.miniButton))
				//{

				//}
			//}*/

			//TODO: when we edit a repo we want to remove and re-download it.
			//TODO: or re-update it if the url and root folder has not changed
			//TODO: and update the gitignore as expected!
		}

		private void DrawUpdateButton(Rect rect)
		{
			if (GUI.Button(rect, new GUIContent("Update", "Pull or clone. Copy into project."), EditorStyles.miniButton))
			{
				//TODO: why is this button never being called?!?!
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