using System;
using System.IO;
using UnityEngine;

namespace Warehouse.Levels
{
    /// <summary>
    /// Resolves the two on-disk folders that sit as close as possible to the
    /// project / build entry point:
    ///   &lt;root&gt;/Working on projects   (levels being edited)
    ///   &lt;root&gt;/Active levels          (exported, playable levels)
    ///
    /// &lt;root&gt; is the parent of Application.dataPath, which is the project root in
    /// the editor and the folder next to the executable in a build.
    /// </summary>
    public static class LevelPaths
    {
        public const string WorkingFolderName = "Working on projects";
        public const string ActiveFolderName = "Active levels";
        public const string Extension = ".json";

        public static string Root
        {
            get
            {
                string assets = Application.dataPath;
                DirectoryInfo parent = Directory.GetParent(assets);
                return parent != null ? parent.FullName : assets;
            }
        }

        public static string WorkingDir
        {
            get { return Path.Combine(Root, WorkingFolderName); }
        }

        public static string ActiveDir
        {
            get { return Path.Combine(Root, ActiveFolderName); }
        }

        public static void EnsureFolders()
        {
            try
            {
                Directory.CreateDirectory(WorkingDir);
                Directory.CreateDirectory(ActiveDir);
            }
            catch (Exception e)
            {
                Debug.LogWarning("LevelPaths.EnsureFolders: " + e.Message);
            }
        }

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Untitled";

            string cleaned = name.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                cleaned = cleaned.Replace(c, '_');
            return cleaned;
        }

        public static string FileNameFor(string levelName, bool active)
        {
            string dir = active ? ActiveDir : WorkingDir;
            return Path.Combine(dir, SanitizeFileName(levelName) + Extension);
        }

        public static string DisplayName(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        public static string[] ListWorking()
        {
            return List(WorkingDir);
        }

        public static string[] ListActive()
        {
            return List(ActiveDir);
        }

        private static string[] List(string dir)
        {
            try
            {
                if (!Directory.Exists(dir))
                    return Array.Empty<string>();

                string[] files = Directory.GetFiles(dir, "*" + Extension);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                return files;
            }
            catch (Exception e)
            {
                Debug.LogWarning("LevelPaths.List: " + e.Message);
                return Array.Empty<string>();
            }
        }
    }
}
