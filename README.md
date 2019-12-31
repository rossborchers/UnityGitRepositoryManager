# UnityGitRepositoryManager

A git repository manager for Unity with a focus on ease of use and fast iteration. 
Allows you to include repositories or subfolders of repositories from other projects in your unity project, update and modify them.

![example gif][(https://media.giphy.com/media/Xc4mNuPuVM1Cb3kdCi/giphy.gif)

- Add and update repositories
- Make modifications to repositories and copy changes back to local repo
- Keeps track of local changes
- A simple UI for handling repositories

This does not satisfy the same use case as UPM (Unity package manager). The intention is for this to be used in paralell with the Unity Package Manager. This is for in-development or project oriented repositories, It allows for us to keep modular and atomic repositories that are reusable, while maintaining the ease of use of a single project workflow. Packages should still be used for third party or stable assets.

This is a tool for working on many repositories at once and pushing changes back. The source can be committed to the main project as well, Although if you keep the repositories up to date there is no need for that.

# Installation
## UPM
- Open ...
## Manual Install
- Download this repository and copy Assets/* into your project. 
  - Open the manager window under "Window/Repository Manager"
  - (Optional) After copying in the project you can also add this repository to the repo manager to have it bootstrap itself, then delete the 

# Technology

- Currently only supports [Windows](https://www.microsoft.com/en-us/software-download/windows10) (should be quite easy to add mac and linux support if anyone is interested)
- Tested with [Unity 2019.2](https://unity.com/)
- c# [.NET standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
- Library uses [LibGit2Sharp](https://github.com/libgit2/libgit2sharp/) 

