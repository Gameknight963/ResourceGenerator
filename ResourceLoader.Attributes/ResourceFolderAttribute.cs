using System;

namespace ResourceLoader.Attributes
{
    /// <summary>
    /// Marks a partial class for resource generation. The generator will scan the specified
    /// folder at build time and emit strongly typed static properties for each recognized file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ResourceFolderAttribute : Attribute
    {
        /// <summary>The path to scan for resource files, relative to the project directory.</summary>
        public string ScanPath { get; }

        /// <summary>The name of the field or property on the class that holds the runtime base path.</summary>
        public string RuntimePath { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ResourceFolderAttribute"/>.
        /// </summary>
        /// <param name="scanPath">The path to scan for resource files, relative to the project directory.</param>
        /// <param name="runtimePath">The name of the field or property that holds the runtime base path.</param>
        public ResourceFolderAttribute(string scanPath, string runtimePath)
        {
            ScanPath = scanPath;
            RuntimePath = runtimePath;
        }
    }
}
