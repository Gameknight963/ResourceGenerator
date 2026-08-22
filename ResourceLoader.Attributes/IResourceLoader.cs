namespace ResourceLoader.Attributes
{
    public interface IResourceLoader<T>
    {
        T Load(string fullPath);
    }
}
