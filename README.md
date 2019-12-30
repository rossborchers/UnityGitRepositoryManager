# UnityGitRepositoryManager

A git "package" manager for Unity with a focus on ease of use and fast iteration. 
Allows you to include repositories or subfolders of repositories from other projects in your unity project. 

- Add and update repositories
- Make modifications to repositories and copy changes back to local repo
- Keeps track of changes
- A simple UI for handling repositories

I've found tools like the Unity package manager, Nuget for Unity or Projeny to be quite slow to use on a fast-changing codebase.
This is a tool for working on many repositories at once and pushing changes back.

The intention is for this to be used in paralell with the Unity Package Manager. Packages for third party or stable assets, repositories for in-development or project oriented assets. 

- Currently only supports [Windows](https://www.microsoft.com/en-us/software-download/windows10) (should be quite easy to add mac and linux support if anyone is interested)
- Tested with [Unity 2019.2](https://unity.com/)
- c# [.NET standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
- Library uses [LibGit2Sharp](https://github.com/libgit2/libgit2sharp/) 

