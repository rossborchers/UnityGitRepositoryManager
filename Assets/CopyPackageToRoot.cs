using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CopyPackageToRoot
{
	/*[CreateAssetMenu("Copy Package To Root")]
	public static void CopyPackageToRoot()
	{
		string root = Application.dataPath.Replace("/Assets", "");

		if (!Directory.Exists(_state.LocalDestination))
		{
			return false;
		}

		Directory.Delete(_state.CopyDestination, true);
		return true;

		// Modified from https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories?redirectedfrom=MSDN
		void DirectoryCopy(string sourceDirName, string destDirName)
		{
			// Get the subdirectories for the specified directory.
			DirectoryInfo dir = new DirectoryInfo(sourceDirName);

			if (!dir.Exists)
			{
				throw new DirectoryNotFoundException(
					"Source directory does not exist or could not be found: "
					+ sourceDirName);
			}

			DirectoryInfo destDir = new DirectoryInfo(destDirName);

			DirectoryInfo[] dirs = dir.GetDirectories();

			// If the destination directory doesn't exist, create it.
			if (!Directory.Exists(destDirName))
			{
				Directory.CreateDirectory(destDirName);
			}

			// Get the files in the directory and copy them to the new location.
			FileInfo[] files = dir.GetFiles();
			foreach (FileInfo file in files)
			{
				string temppath = Path.Combine(destDirName, file.Name);

				if (File.Exists(temppath))
				{
					File.SetAttributes(temppath, FileAttributes.Normal);
				}

				file.CopyTo(temppath, true);
			}

			//Copy sub directories
			foreach (DirectoryInfo subdir in dirs)
			{
				string temppath = Path.Combine(destDirName, subdir.Name);
				DirectoryCopy(subdir.FullName, temppath);
			}
		}
	}*/
}
