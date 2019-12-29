using System;

namespace GitRepositoryManager
{
	[Serializable]
	public class Dependency
	{
		public string Name = string.Empty;
		public string Url = string.Empty;
		public string SubFolder = string.Empty;
		public string Branch = "master";
		//public string Tag = "";

		public Requirement[] Requires = null;
	}

	public class Requirement
	{
		public string Name = null;
		public string Url = string.Empty;
		public string SubFolder = string.Empty;
	}
}