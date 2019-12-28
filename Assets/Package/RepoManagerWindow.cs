using GitRepositoryManager;
using GitRepositoryManager.CredentialManagers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

public class RepoManagerWindow : EditorWindow
{
	private static RepoManagerWindow _window;

	[Serializable]
	private class DependencyInfo
	{
		public List<Dependency> Dependencies = new List<Dependency>();
	}

	private string _repoPath;
	private string _rootDependenciesFile;
	private DependencyInfo _dependencies;
	private Dependency _potentialNewDependency = new Dependency();

	private AnimBool _showAddDependencyMenu;

	private List<RepoPanel> _repoPanels;

	private RepositoryTester _tester;

	private double _addTime;
	private float _minimumAddTimeBeforeFeedback = 0.25f;
	private string addDependencyFailureMessage;
	private bool showWarningMessage;
	private bool lastFrameWasWaitingToShowWarning;

	[MenuItem("Window/Repository Manager", priority = 1500)]
	static void Init()
	{
		_window = (RepoManagerWindow)GetWindow(typeof(RepoManagerWindow));
		_window.titleContent = new GUIContent("Repository Manager");
		_window.Show();
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
		if(updatedDependencies != null)
		{
			//Remove no longer existing
			foreach (var dep in _dependencies.Dependencies)
			{
				if (updatedDependencies.FindAll(d => d.Url == dep.Url).Count <= 0)
				{
					_dependencies.Dependencies.Remove(dep);
				}
			}

			//Add new
			foreach (var dep in updatedDependencies)
			{
				if(_dependencies.Dependencies.FindAll(d => d.Url == dep.Url).Count <= 0)
				{
					_dependencies.Dependencies.Add(dep);
				}
			}

			json = JsonUtility.ToJson(_dependencies, true);
			File.WriteAllText(_rootDependenciesFile, json);
		}

		//Update repo panels
		_repoPanels = new List<RepoPanel>();
		foreach (Dependency dependency in _dependencies.Dependencies)
		{
			if (_repoPanels.FindAll(p => dependency.Url == p.DependencyInfo.Url).Count == 0)
			{
				_repoPanels.Add(new RepoPanel(dependency));
			}
		}

		Repaint();
	}

	private void OnFocus()
	{
		UpdateDependencies();
	}

	private void OnEnable()
	{
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

		if(_tester.Testing)
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

		foreach(RepoPanel panel in _repoPanels)
		{
			if(panel.Update())
			{
				repaint = true;
			}
		}

		if(repaint)
		{
			Repaint();
		}
	}

	private ICredentialManager GetPlatformAuthentication()
	{
		return new WindowsCredentialManager();
	}

	private void OnGUI()
	{
		foreach(RepoPanel panel in _repoPanels)
		{
			Rect repoRect = EditorGUILayout.GetControlRect();
			panel.OnDrawGUI(repoRect);
		}

		//This is a dumb but kidna fancy way of adding new dependencies.
		if(_tester.Testing)
		{
			GUI.enabled = false;
		}

		if (EditorGUILayout.BeginFadeGroup(_showAddDependencyMenu.faded))
		{
			_potentialNewDependency.Name = EditorGUILayout.TextField("Name", _potentialNewDependency.Name);
			_potentialNewDependency.Url = EditorGUILayout.TextField("Url", _potentialNewDependency.Url);
			_potentialNewDependency.SubDirectory = EditorGUILayout.TextField("SubDirectory", _potentialNewDependency.SubDirectory);
			_potentialNewDependency.Branch = EditorGUILayout.TextField("Branch", _potentialNewDependency.Branch);
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
			if (GUI.Button(addButtonRect, _showAddDependencyMenu.target ? "Add" : "Add Dependency"))
			{
				addDependencyFailureMessage = string.Empty;
				_addTime = EditorApplication.timeSinceStartup;

				if (!_showAddDependencyMenu.target)
				{
					_showAddDependencyMenu.target = true;
				}
				else
				{
					_tester.Test(_potentialNewDependency.Url, GetPlatformAuthentication(), (success, message) =>
					{
						if (success)
						{
							_dependencies.Dependencies.Add(_potentialNewDependency);
							UpdateDependencies(_dependencies.Dependencies);
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
		else
		{
			string dots = string.Empty;
			int dotCount = Mathf.FloorToInt((float)(EditorApplication.timeSinceStartup % 3))+1;
			for (int i = 0; i < dotCount; i++) { dots += "."; }
			GUI.Label(addButtonRect, "Testing connection" + dots + "\n" + _potentialNewDependency.Url, EditorStyles.boldLabel);
		}

		GUI.enabled = true;

		if (_showAddDependencyMenu.target)
		{
			if(GUI.Button(cancelButtonRect, "Cancel"))
			{
				CloseAddMenu();
			}
		}

		if(Event.current.type == EventType.Layout)
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
