using System;

namespace ResourceLoader.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterLoaderAttribute : Attribute
    {
        public Type LoaderType { get; }

        public RegisterLoaderAttribute(Type loaderType)
        {
            LoaderType = loaderType;
        }
    }
}
