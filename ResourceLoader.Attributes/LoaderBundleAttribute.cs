using System;

namespace ResourceLoader.Attributes
{
    /// <summary>
    /// Marks an attribute class as a loader bundle. Bundle attributes can carry
    /// <see cref="RegisterLoaderAttribute"/>s and be applied to a resource class to
    /// register multiple loaders at once.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class LoaderBundleAttribute : Attribute { }
}
