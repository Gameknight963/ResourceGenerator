namespace ResourceLoader.Core
{
    public interface IResourceLoader<T>
    {
        T Load(string fullPath);
    }
}
