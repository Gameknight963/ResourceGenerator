using System;

namespace ResourceLoader.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class HandlesExtensionsAttribute : Attribute
    {
        public string[] Extensions { get; }

        public HandlesExtensionsAttribute(params string[] extensions)
        {
            Extensions = extensions;
        }
    }
}
