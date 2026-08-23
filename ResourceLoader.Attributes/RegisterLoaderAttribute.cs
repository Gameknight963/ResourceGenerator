using System;

namespace ResourceLoader.Attributes
{
    /// <summary>
    /// Registers a custom loader with the resource generator for the annotated class.
    /// The loader must implement <see cref="ResourceLoader.Core.IResourceLoader{T}"/> and
    /// be annotated with <see cref="HandlesExtensionsAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RegisterLoaderAttribute : Attribute
    {
        /// <summary>The loader type to register.</summary>
        public Type LoaderType { get; }

        /// <summary>
        /// Whether this loader should override a loader for the same extension provided by a bundle.
        /// If <see langword="false"/> and a conflict exists, an error diagnostic will be emitted.
        /// </summary>
        public bool OverrideBundle { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="RegisterLoaderAttribute"/>.
        /// </summary>
        /// <param name="loaderType">The loader type to register.</param>
        /// <param name="overrideBundle">Whether to override conflicting bundle loaders.</param>
        public RegisterLoaderAttribute(Type loaderType, bool overrideBundle = false)
        {
            LoaderType = loaderType;
            OverrideBundle = overrideBundle;
        }
    }
}
