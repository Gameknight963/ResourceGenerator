using System;

namespace ResourceLoader.Attributes
{
    /// <summary>
    /// When applied to a <see cref="ResourceLoader.Core.IResourceLoader{T}"/>, marks it as non-caching
    /// (loads files every time they're accessed)
    /// </summary>
    public class NonCachingAttribute : Attribute { }
}
