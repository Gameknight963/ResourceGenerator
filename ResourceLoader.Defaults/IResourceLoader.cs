namespace ResourceLoader.Defaults
{
    public interface IResourceLoader<T>
    {
        T Load(string fullPath);
    }
}
