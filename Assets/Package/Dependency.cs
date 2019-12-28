using System;

[Serializable]
public class Dependency
{
	public string Name = string.Empty;
	public string Url = "https://github.com/...";
	public string SubDirectory = string.Empty;
	public string Branch = "master";
}