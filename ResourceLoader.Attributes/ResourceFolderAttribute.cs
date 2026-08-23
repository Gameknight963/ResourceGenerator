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
        /// Whether to recursively scan subdirectories. When <see langword="true"/>, files in
        /// subdirectories are emitted as properties on nested static classes mirroring the
        /// folder structure. Defaults to <see langword="false"/>.
        /// </summary>
        public bool Recursive { get; } = false;

        /// <summary>
        /// Initializes a new instance of <see cref="ResourceFolderAttribute"/>.
        /// </summary>
        /// <param name="scanPath">The path to scan for resource files, relative to the project directory.</param>
        /// <param name="runtimePath">The name of the field or property that holds the runtime base path.</param>
        /// <param name="recursive">
        /// Whether to recursively scan subdirectories. When <see langword="true"/>, files in
        /// subdirectories are emitted as properties on nested static classes mirroring the
        /// folder structure.
        /// </param>
        public ResourceFolderAttribute(string scanPath, string runtimePath, bool recursive = false)
        {
            ScanPath = scanPath;
            RuntimePath = runtimePath;
            Recursive = recursive;
        }
    }
}
