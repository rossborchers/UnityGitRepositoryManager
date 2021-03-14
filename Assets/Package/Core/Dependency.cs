using System;

namespace GitRepositoryManager
{
	[Serializable]
	public class Dependency
	{
		public string Name = string.Empty;
		public string SubFolder = string.Empty;

		public string Url = string.Empty;
		public string Branch = "main";
	}
}