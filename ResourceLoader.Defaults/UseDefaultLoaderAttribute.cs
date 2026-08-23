using ResourceLoader.Attributes;
using System;

namespace ResourceLoader.Defaults
{
    /// <summary>
    /// A loader bundle that registers <see cref="TextLoader"/> and <see cref="BytesLoader"/>
    /// as default loaders for common file types.
    /// </summary>
    [LoaderBundle]
    [RegisterLoader(typeof(TextLoader))]
    [RegisterLoader(typeof(BytesLoader))]
    public sealed class UseDefaultLoadersAttribute : Attribute { }
}
