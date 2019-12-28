using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RepoPanel
{
	public Dependency DependencyInfo
	{
		get;
		private set;
	}

    public RepoPanel(Dependency info)
	{
		DependencyInfo = info;
	}

	//TODO: link to repository task needs to survive editor reloading in case job is in progress. (or job needs to block exit somehow)

	//TODO: also figure out multi column stuff if required. 

	public bool Update()
	{
		return true;
	}

	public void OnDrawGUI(Rect rect)
	{
		GUI.Label(rect, DependencyInfo.Name);
	}
}
