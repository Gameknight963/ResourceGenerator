using ResourceLoader.Attributes;
using ResourceLoader.Core;
using System.IO;

namespace ResourceLoader.Defaults
{
    /// <summary>
    /// Loads text files as a <see cref="string"/> using the default encoding.
    /// </summary>
    [HandlesExtensions(".txt", ".json", ".xml", ".yaml", ".yml")]
    public sealed class TextLoader : IResourceLoader<string>
    {
        /// <inheritdoc/>
        public string Load(string fullPath) => File.ReadAllText(fullPath);
    }
}
