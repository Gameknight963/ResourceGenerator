namespace ResourceLoader.Core
{
    /// <summary>
    /// Defines a mechanism for loading a file from disk into a strongly-typed value.
    /// </summary>
    /// <typeparam name="T">The type of value produced by this loader.</typeparam>
    public interface IResourceLoader<T>
    {
        /// <summary>
        /// Loads a file from the specified path and returns the resulting value.
        /// </summary>
        /// <param name="fullPath">The full path to the file on disk.</param>
        /// <returns>The loaded <typeparamref name="T"/>.</returns>
        T Load(string fullPath);
    }
}
