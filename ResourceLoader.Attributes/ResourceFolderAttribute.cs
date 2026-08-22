using System;

namespace ResourceLoader.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ResourceFolderAttribute : Attribute
    {
        public string ScanPath { get; }
        public string RuntimePath { get; }

        public ResourceFolderAttribute(string scanPath, string runtimePath)
        {
            ScanPath = scanPath;
            RuntimePath = runtimePath;
        }
    }
}
