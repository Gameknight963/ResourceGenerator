using ResourceLoader.Attributes;
using System.IO;

namespace ResourceLoader.Defaults
{
    [HandlesExtensions("*")]
    [WarnIfTransitive]
    public sealed class BytesLoader : IResourceLoader<byte[]>
    {
        public byte[] Load(string fullPath) => File.ReadAllBytes(fullPath);
    }
}
