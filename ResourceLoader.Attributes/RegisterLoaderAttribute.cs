using System;

namespace ResourceLoader.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RegisterLoaderAttribute : Attribute
    {
        public Type LoaderType { get; }
        public bool OverrideBundle { get; }

        public RegisterLoaderAttribute(Type loaderType, bool overrideBundle = false)
        {
            LoaderType = loaderType;
            OverrideBundle = overrideBundle;
        }
    }
}
