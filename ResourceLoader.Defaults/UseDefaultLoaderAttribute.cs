using ResourceLoader.Attributes;
using System;

namespace ResourceLoader.Defaults
{
    [LoaderBundle]
    [RegisterLoader(typeof(TextLoader))]
    [RegisterLoader(typeof(BytesLoader))]
    public sealed class UseDefaultLoadersAttribute : Attribute { }
}
