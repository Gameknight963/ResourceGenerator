using ResourceLoader.Attributes;
using ResourceLoader.Core;
using System.IO;

namespace ResourceLoader.Defaults
{
    [HandlesExtensions(".txt", ".json", ".xml", ".yaml", ".yml")]
    public sealed class TextLoader : IResourceLoader<string>
    {
        public string Load(string fullPath) => File.ReadAllText(fullPath);
    }
}
