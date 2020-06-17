 Unity Git Repository Manager

A git repository manager for Unity with a focus on ease of use and fast iteration.
Allows you to include repositories or subfolders of repositories from other projects in your unity project, update and modify them.

This is a tool for working on many repositories at once and pushing changes back.

### Downloading repo

![downloading repo](https://media.giphy.com/media/Xc4mNuPuVM1Cb3kdCi/giphy.gif)

### Resulting file structure

![resulting file structire](https://media.giphy.com/media/TIXo3KWDXnhO3bFZfn/giphy.gif)

# Features

- Add and update repositories
- Make modifications to repositories and copy changes back to the local repo
- Keeps track of local changes
- A simple UI for handling repositories

# Installation
### Simple Install

- Download or clone this repository.
  - copy Assets/Package anywhere under your project's Assets folder.
  - _if you want to contribute to the plugin development you will need to work directly in the cloned repository_

### Manual Self-Bootsrap Install

- Download this repository.
  - copy "Assets/Package" it into an empty directory structure: "Assets/Repositories/RepositoryManager"
  - Open the manager window under "Window/Repository Manager"
  - Add https://github.com/rossborchers/UnityGitRepositoryManager with the name RepositoryManager
  - Update it.
  - You may need to restart Unity.
  - You now have a self bootstrapping repo manager. You can make changes and pr them back here ;)

### UPM

- Right now there is no support for UPM as we would want to only package the "Package" subfolder which as of the time of writing this, it does not support. Although it is supposedly on the roadmap. :)
- You could make a local package if you wanted to go through the trouble though.

# Rationale

I've constantly run into the issue of decoupling into separate repositories slowing down development. We have multiple repositories for a single project, it gets quite slow.
But when we don't decouple we end up with unmaintainable and un-reusable monoliths. We need a way to quickly iterate on multiple repositories while maintaining their modularity. This is what the repo manager tries to solve.

#### Problems with package managers
Package managers are in my experience geared for a read-only workflow, so they don't satisfy the same use case ([upm](https://docs.unity3d.com/Manual/upm-parts.html), [projeny](https://github.com/modesttree/projeny), [nuget](https://github.com/GlitchEnzo/NuGetForUnity)). I want it to be quick to update, have dependency management, but more importantly, be able to quickly __push changes__

#### Problems with git submodules
Git submodules are the closest thing to this. It would be _loveley_ if it worked but there is one big issue:
git submodules **[do not](https://stackoverflow.com/questions/5303496/how-to-change-a-git-submodule-to-point-to-a-subfolder) [support](https://www.reddit.com/r/git/comments/8sanj7/create_subfolder_using_a_subfolder_from_a/) pulling only subfolders**.
##### Why do you need to pull subfolders?
- Often when developing plugins the plugin project itself is located in the same repository.
- We sometimes have test scenes or other irrelevant assets we do not want to include. These bloat the dependents.
- In a unity project, we have the folders that exist outside (ProjectSettings, etc.) of assets. We don't want to include those in a project that uses it as a submodule

I've heard people argue that when you need a subfolder what you need to do is split into 2 repositories. but just because you want to share your assets folder separately to your project does not mean that they are decoupled. In the name of cohesion and sanity, this is a bad idea!

##### Git sparse checkout
Since making this project sparse checkout has been released. This combined with submodules may be a decent alternative.

### In Summary
Updating multiple repositories is slow, monoliths are bad. We need a way to update multiple repositories fast.
The use case I've described does not satisfy the same use case as UPM or any other package managers I'm aware of. The intention is for this to be used in parallel with the Unity Package Manager. This is for in-development or project-oriented repositories, It allows for us to keep modular and atomic repositories that are reusable while maintaining the ease of use and mutability of a single project workflow.

_Packages should still be used for third party projects or projects no longer in active development!_

# Technology

- Currently only supports [Windows](https://www.microsoft.com/en-us/software-download/windows10) (should be quite easy to add mac and Linux support if anyone is interested)
- Tested with [Unity 2019.2](https://unity.com/)
- c# [.NET standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
- Library uses [LibGit2Sharp](https://github.com/libgit2/libgit2sharp/). Tested on public and private repos. Supports 2FA.

# Contributing

This project is in the early stages, there are many easy bugs to squash and many features that would be useful. Ill outline issues as they become apparent.

If you give it a try, use it on a project, or just want to discuss the ideology of this approach please email me.
