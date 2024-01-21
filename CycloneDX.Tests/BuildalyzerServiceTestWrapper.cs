using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Services;

namespace CycloneDX.Tests
{
    /// <summary>
    /// Buildalyzer doesn't work with System.IO.Abstraction. So we dump it all on the regular FS for it and translate all incoming paths.
    /// </summary>
    public class BuildalyzerServiceTestWrapper : IBuildalyzerService
    {
        readonly BuildalyzerService coreBuildAnalyzerService = new();
        private string _tempFolderPath;
        readonly MockFileSystem fileSystem;

        public BuildalyzerServiceTestWrapper(MockFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
            DumpFilesystemOnDisk();
        }

        readonly Dictionary<string, string> reversePathMap = [];


        void DumpFilesystemOnDisk()
        {
            // Create a temporary folder to hold the mock file system
            _tempFolderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempFolderPath);

            // Copy all files and directories from the mock file system to the temp folder
            foreach (var file in fileSystem.AllFiles)
            {
                string tempFilePath = TranslatePath(file);
                string directoryPath = Path.GetDirectoryName(tempFilePath);

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.WriteAllText(tempFilePath, fileSystem.File.ReadAllText(file));
                reversePathMap.Add(tempFilePath, file);
            }
        }

        private string TranslatePath(string originalPath)
        {
            // Translate the original path from the mock file system to the corresponding path in the temp folder
            return Path.Combine(_tempFolderPath, fileSystem.Path.GetRelativePath(fileSystem.Path.GetPathRoot(originalPath), originalPath));
            
             
        }

        public string TranslatePathBackwards(string tempPath)
        {
            return reversePathMap[tempPath];
        }

        public HashSet<string> GetProjectPathsOfSolution()
        {
            return coreBuildAnalyzerService
                        .GetProjectPathsOfSolution()
                            .Select(TranslatePathBackwards)
                            .ToHashSet();
        }

        public void InitializeAnalyzer(string solutionFilePath)
        {
            coreBuildAnalyzerService.InitializeAnalyzer(TranslatePath(solutionFilePath));
        }

        public bool IsTestProject(string projectFilePath)
        {
            return coreBuildAnalyzerService.IsTestProject(TranslatePath(projectFilePath));
        }

        ~BuildalyzerServiceTestWrapper()
        {
            CleanUpTempFolder();
        }

        private void CleanUpTempFolder()
        {
            try
            {
                if (Directory.Exists(_tempFolderPath))
                {
                    Directory.Delete(_tempFolderPath, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.GetType()} when deleting {_tempFolderPath}\r\n{ex.Message}");
            }
        }
    }
}
