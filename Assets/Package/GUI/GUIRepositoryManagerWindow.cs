using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.Callbacks;
using UnityEditor.Graphs;
using UnityEngine;

namespace GitRepositoryManager
{
	public class GUIRepositoryManagerWindow : EditorWindow
	{
		private static GUIRepositoryManagerWindow _window;

		[Serializable]
		private class DependencyInfo
		{
			public List<Dependency> Dependencies = new List<Dependency>();
		}

		private string _repoPath;
		private string _rootDependenciesFile;
		private DependencyInfo _dependencies;
		private Dependency _potentialNewDependency = new Dependency();
		//	private int selectedFilterIndex;

		private AnimBool _showAddDependencyMenu;

		private List<GUIRepositoryPanel> _repoPanels;

		private RepositoryTester _tester;

		private double _addTime;
		private float _minimumAddTimeBeforeFeedback = 0.25f;
		private string addDependencyFailureMessage;
		private bool showWarningMessage;
		private bool lastFrameWasWaitingToShowWarning;

		private HashSet<GUIRepositoryPanel> _reposWereBusy = new HashSet<GUIRepositoryPanel>();
		private HashSet<GUIRepositoryPanel> _reposBusy = new HashSet<GUIRepositoryPanel>();

		[MenuItem("Window/Repository Manager", priority = 1500)]
		static void Init()
		{
			//Find other windows to dock to by default.
			List<Type> types = new List<Type>();
			EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
			foreach (EditorWindow window in allWindows)
			{
				//Can see the project view still in most configurations, and the scene tree is shown vertically. alternatives could be project view or inspector.
				if (window.GetType().Name == "SceneHierarchyWindow")
				{
					types.Add(window.GetType());
				}
			}

			_window = (GUIRepositoryManagerWindow)GetWindow<GUIRepositoryManagerWindow>(types.ToArray());
			_window.titleContent = new GUIContent("Repository Manager");
			_window.Show();
		}

		private Texture2D _editIcon;
		private Texture2D _removeIcon;
		private Texture2D _pushIcon;
		private Texture2D _pullIcon;
		private Texture2D _openTerminalIcon;

		private void LoadAssets()
		{
			_editIcon = Resources.Load("RepositoryManager/" + "Edit") as Texture2D;
			_removeIcon = Resources.Load("RepositoryManager/" + "Remove") as Texture2D;
			_pushIcon = Resources.Load("RepositoryManager/" + "Push") as Texture2D;
			_pullIcon = Resources.Load("RepositoryManager/" + "Pull") as Texture2D;
			_openTerminalIcon = Resources.Load("RepositoryManager/" + "Terminal") as Texture2D;
		}

		private void UpdateDependencies(List<Dependency> updatedDependencies = null)
		{
			_repoPath = Path.Combine(Application.dataPath, "Repositories");
			_rootDependenciesFile = Path.Combine(_repoPath, "Dependencies.json");

			//Ensure to create directory structure and default dependencies if nothing exists
			Directory.CreateDirectory(_repoPath);
			if (!File.Exists(_rootDependenciesFile)) { File.WriteAllText(_rootDependenciesFile, JsonUtility.ToJson(new DependencyInfo(), true)); }
			string json = File.ReadAllText(_rootDependenciesFile);
			_dependencies = JsonUtility.FromJson<DependencyInfo>(json);

			//Sync file dependencies with in memory dependencies
			if (updatedDependencies != null)
			{
				//Remove no longer existing
				for (int i = _dependencies.Dependencies.Count - 1; i >= 0; i--)
				{
					var dep = _dependencies.Dependencies[i];
					if (updatedDependencies.FindAll(d => d.Url == dep.Url).Count <= 0)
					{
						_dependencies.Dependencies.RemoveAt(i);
					}
				}

				//Add new
				foreach (var dep in updatedDependencies)
				{
					if (_dependencies.Dependencies.FindAll(d => d.Url == dep.Url).Count <= 0)
					{
						_dependencies.Dependencies.Add(dep);
					}
				}

				json = JsonUtility.ToJson(_dependencies, true);
				File.WriteAllText(_rootDependenciesFile, json);
			}

			//Update repo panels
			_repoPanels = new List<GUIRepositoryPanel>();
			foreach (Dependency dependency in _dependencies.Dependencies)
			{
				if (_repoPanels.FindAll(p => dependency.Url == p.DependencyInfo.Url).Count == 0)
				{
					GUIRepositoryPanel panel = new  GUIRepositoryPanel(dependency, _editIcon, _removeIcon, _pushIcon, _pullIcon, _openTerminalIcon);
					panel.OnRemovalRequested += OnPanelRemovalRequested;
					panel.OnRefreshRequested += (updatedRepos) =>
					{
						UpdateAssetDatabaseAndTakeSnapshots(updatedRepos);
					};
					panel.OnEditRequested += OnPanelEditRequested;
					_repoPanels.Add(panel);
				}
			}
			Repaint();
		}

		private void OnPanelEditRequested(Dependency dependencyInfo, string path)
		{
			_potentialNewDependency.Url = dependencyInfo.Url;
			_potentialNewDependency.Branch = dependencyInfo.Branch;
			_potentialNewDependency.Name = dependencyInfo.Name;
			_potentialNewDependency.SubFolder = dependencyInfo.SubFolder;
			_showAddDependencyMenu.target = true;
		}

		private void UpdateAssetDatabaseAndTakeSnapshots(params GUIRepositoryPanel[] updatedRepos)
		{
			EditorUtility.DisplayProgressBar("Importing Repositories", "Performing re-import" + GUIUtility.GetLoadingDots(), (float)EditorApplication.timeSinceStartup % 1);
			AssetDatabase.Refresh();

			//snapshot folder and file state to compare against later!
			foreach (GUIRepositoryPanel panel in updatedRepos)
			{
				panel.TakeBaselineSnapshot();
			}
			EditorUtility.ClearProgressBar();
		}

		private void OnPanelRemovalRequested(string name, string url, string repoPath)
		{
			EditorUtility.DisplayProgressBar("Removing Repository", $"Removing {name} and re-importing" + GUIUtility.GetLoadingDots(), (float)EditorApplication.timeSinceStartup % 1);

			for (int i = 0; i < _dependencies.Dependencies.Count; i++)
			{
				if (_dependencies.Dependencies[i].Name == name && _dependencies.Dependencies[i].Url == url)
				{
					GUIRepositoryPanel panel = _repoPanels.Find(p => _dependencies.Dependencies[i].Url == p.DependencyInfo.Url);
					_dependencies.Dependencies.RemoveAt(i);
					Repository.Remove(url, repoPath);
				}
			}

			//TODO: if we could referesh a folder only that would be nice. cant reimport assets that dont exists.
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

			UpdateDependencies(_dependencies.Dependencies);

			EditorUtility.ClearProgressBar();
		}

		private void OnFocus()
		{
			UpdateDependencies();
		}

		private void OnEnable()
		{
			LoadAssets();
			UpdateDependencies();

			_showAddDependencyMenu = new AnimBool(false);
			_showAddDependencyMenu.valueChanged.AddListener(Repaint);

			_tester = new RepositoryTester();
			EditorApplication.update += UpdateEditor;
		}

		private void OnDisable()
		{
			EditorApplication.update -= UpdateEditor;
		}

		private void UpdateEditor()
		{
			bool repaint = false;

			if (_tester.Testing)
			{
				_tester.Update();
				repaint = true;
			}

			//All just to get a damn warning message updating properly.
			double timeSinceButtonPress = EditorApplication.timeSinceStartup - _addTime;
			if (timeSinceButtonPress < _minimumAddTimeBeforeFeedback)
			{
				lastFrameWasWaitingToShowWarning = true;
			}
			else if (lastFrameWasWaitingToShowWarning)
			{
				repaint = true;
				lastFrameWasWaitingToShowWarning = false;
			}

			_reposBusy.Clear();
			foreach (GUIRepositoryPanel panel in _repoPanels)
			{
				if (panel.Update())
				{
					repaint = true;
				}

				if (panel.Busy())
				{
					_reposBusy.Add(panel);
				}
			}

			List<GUIRepositoryPanel> updatedRepos = new List<GUIRepositoryPanel>();

			//Repos just finished updating. Time to copy.
			foreach (GUIRepositoryPanel panel in _repoPanels)
			{
				//check what repos just finished updating
				if (_reposWereBusy.Contains(panel) && !_reposBusy.Contains(panel))
				{
					updatedRepos.Add(panel);
				}
			}

			if (repaint)
			{
				Repaint();
			}

			_reposWereBusy = new HashSet<GUIRepositoryPanel>(_reposBusy);
		}

		private void OnGUI()
		{
			Rect labelRect = EditorGUILayout.GetControlRect();
			labelRect.y += labelRect.height / 2f;

			Rect updateAllRect = labelRect;
			updateAllRect.width = 70;
			updateAllRect.height = 15;
			updateAllRect.x = labelRect.width - 70;

			Rect cancelAllRect = labelRect;
			cancelAllRect.width = 70;
			cancelAllRect.height = 15;
			cancelAllRect.x = labelRect.width - (70 * 2);

			GUI.Label(labelRect, "Repositories", EditorStyles.miniLabel);

			List<bool> localChangesFlags = new List<bool>(_repoPanels.Count);
			foreach (var panel in _repoPanels)
			{
				localChangesFlags.Add(panel.HasLocalChanges(true));
			}

			if (GUI.Button(updateAllRect, new GUIContent("Update All", "Update all. You will be asked before overwriting local changes."), EditorStyles.toolbarButton))
			{
				bool localChangesDetected = false;
				for (int i = 0; i < _repoPanels.Count; i++)
				{
					if(localChangesFlags[i])
					{
						localChangesDetected = true;
						break;
					}
				}

				if(localChangesDetected)
				{
					int choice = EditorUtility.DisplayDialogComplex("Local Changes Detected", "One or more repositories have local changes. Proceed with caution.", "Update and wipe all changes",  "Cancel", "Update only clean submodules");
					switch(choice)
					{
						case 0:
						{
							//Update and wipe all.
							for (int i = 0; i < _repoPanels.Count; i++)
							{
								_repoPanels[i].UpdateRepository();
							}
							break;
						}
						case 1:
						{
							//Do nothing on cancel.
							break;
						}
						case 2:
						{
							//Update only clean
							for (int i = 0; i < _repoPanels.Count; i++)
							{
								if (!localChangesFlags[i])
								{
									_repoPanels[i].UpdateRepository();
								}
							}
							break;
						}
					}
				}
				else
				{
					for (int i = 0; i < _repoPanels.Count; i++)
					{
						_repoPanels[i].UpdateRepository();
					}
				}
			}

			GUIUtility.DrawLine();

			for (int i = 0; i < _repoPanels.Count; i++)
			{
				_repoPanels[i].OnDrawGUI(i);
			}


			GUIUtility.DrawLine();


			//This is a dumb but kidna fancy way of adding new dependencies.
			if (_tester.Testing)
			{
				GUI.enabled = false;
			}

			if (EditorGUILayout.BeginFadeGroup(_showAddDependencyMenu.faded))
			{
				_potentialNewDependency.Url = EditorGUILayout.TextField("Url", _potentialNewDependency.Url);

				if (_potentialNewDependency.Branch == null) _potentialNewDependency.Branch = "main";
				_potentialNewDependency.Branch = EditorGUILayout.TextField("Branch", _potentialNewDependency.Branch);

				EditorGUILayout.Space();

				_potentialNewDependency.Name = EditorGUILayout.TextField("Name", _potentialNewDependency.Name);
				_potentialNewDependency.SubFolder = EditorGUILayout.TextField("Subfolder", _potentialNewDependency.SubFolder);

				EditorGUILayout.Space();
			}
			else
			{
				addDependencyFailureMessage = string.Empty;
			}

			EditorGUILayout.EndFadeGroup();

			Rect addButtonRect = EditorGUILayout.GetControlRect();
			Rect cancelButtonRect = new Rect(addButtonRect);
			if (_showAddDependencyMenu.target)
			{
				addButtonRect = new Rect(addButtonRect.x, addButtonRect.y, addButtonRect.width * 0.6666f, addButtonRect.height);
				cancelButtonRect = new Rect(addButtonRect.x + addButtonRect.width, cancelButtonRect.y, cancelButtonRect.width * 0.33333f, cancelButtonRect.height);
			}

			if (!_tester.Testing)
			{
				if (GUI.Button(addButtonRect, _showAddDependencyMenu.target ? "Confirm" : "Add Repository", EditorStyles.miniButton))
				{
					addDependencyFailureMessage = string.Empty;
					_addTime = EditorApplication.timeSinceStartup;

					if (!_showAddDependencyMenu.target)
					{
						_showAddDependencyMenu.target = true;
					}
					else
					{
						//simple validation of fields
						bool validationSuccess = true;

						if (String.IsNullOrEmpty(_potentialNewDependency.Branch))
						{
							addDependencyFailureMessage = "A valid branch must be specified";
							validationSuccess = false;
						}

						foreach (Dependency dep in _dependencies.Dependencies)
						{
							if (dep.Name.Trim().ToLower() == _potentialNewDependency.Name.Trim().ToLower())
							{
								addDependencyFailureMessage = "Name already exists.";
								validationSuccess = false;
							}
							else if (dep.Url.Trim().ToLower() == _potentialNewDependency.Url.Trim().ToLower())
							{
								addDependencyFailureMessage = "Repository already exists with the current url.\nExisting: " + dep.Name;
								validationSuccess = false;
							}
						}

						if (String.IsNullOrEmpty(_potentialNewDependency.Name))
						{
							addDependencyFailureMessage = "Name can not be empty.";
							validationSuccess = false;
						}

						if (validationSuccess)
						{
							//actually connect to repository
							_tester.Test(_potentialNewDependency.Url, _potentialNewDependency.Branch, _potentialNewDependency.SubFolder, (success, message) =>
							{
								if (success)
								{
									_dependencies.Dependencies.Add(_potentialNewDependency);
									UpdateDependencies(_dependencies.Dependencies);

									//Update the newly added repo
									foreach (GUIRepositoryPanel panel in _repoPanels)
									{
										if (panel.DependencyInfo.Url == _potentialNewDependency.Url &&
											panel.DependencyInfo.Branch == _potentialNewDependency.Branch &&
											panel.DependencyInfo.Name == _potentialNewDependency.Name)
										{
											//force this as we want to copy
											panel.UpdateRepository();
										}
									}

									CloseAddMenu();
								}
								else
								{
									addDependencyFailureMessage = message;
								}
							});
						}
					}
				}
			}
			else
			{
				GUI.Label(addButtonRect, "Testing connection" + GUIUtility.GetLoadingDots(), EditorStyles.boldLabel);
			}

			GUI.enabled = true;

			if (_showAddDependencyMenu.target)
			{
				if (GUI.Button(cancelButtonRect, "Cancel", EditorStyles.miniButton))
				{
					CloseAddMenu();
				}
			}

			if (Event.current.type == EventType.Layout)
			{
				// Give the failure message a slight delay so we can see that the message is new even if its the same.
				double timeSinceButtonPress = EditorApplication.timeSinceStartup - _addTime;
				showWarningMessage = (!string.IsNullOrEmpty(addDependencyFailureMessage) && timeSinceButtonPress > _minimumAddTimeBeforeFeedback);
			}

			if (showWarningMessage)
			{
				EditorGUILayout.HelpBox(addDependencyFailureMessage, MessageType.Warning);
			}
		}

		private void CloseAddMenu()
		{
			addDependencyFailureMessage = string.Empty;
			_showAddDependencyMenu.target = false;
			_potentialNewDependency = new Dependency();
		}
	}
}