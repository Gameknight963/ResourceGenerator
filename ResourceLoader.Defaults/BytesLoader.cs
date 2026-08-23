using ResourceLoader.Attributes;
using ResourceLoader.Core;
using System.IO;

namespace ResourceLoader.Defaults
{
    /// <summary>
    /// Loads any file as a raw <see cref="byte"/> array.
    /// Handles all extensions via the wildcard <c>*</c>.
    /// </summary>
    [HandlesExtensions("*")]
    [WarnIfTransitive]
    public sealed class BytesLoader : IResourceLoader<byte[]>
    {
        /// <inheritdoc/>
        public byte[] Load(string fullPath) => File.ReadAllBytes(fullPath);
    }
}
