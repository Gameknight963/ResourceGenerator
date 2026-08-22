using System;

namespace ResourceLoader.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RegisterLoaderAttribute : Attribute
    {
        public Type LoaderType { get; }

        public RegisterLoaderAttribute(Type loaderType)
        {
            LoaderType = loaderType;
        }
    }
}
