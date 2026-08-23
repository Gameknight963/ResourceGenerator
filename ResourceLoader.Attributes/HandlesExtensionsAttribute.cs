using System;

namespace ResourceLoader.Attributes
{
    /// <summary>
    /// Specifies the file extensions handled by a loader class.
    /// Use <c>*</c> as a wildcard to match any extension not claimed by a more specific loader.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HandlesExtensionsAttribute : Attribute
    {
        /// <summary>The file extensions this loader handles, e.g. <c>.png</c>, <c>.jpg</c>.</summary>
        public string[] Extensions { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="HandlesExtensionsAttribute"/>.
        /// </summary>
        /// <param name="extensions">The file extensions this loader handles.</param>
        public HandlesExtensionsAttribute(params string[] extensions)
        {
            Extensions = extensions;
        }
    }
}
