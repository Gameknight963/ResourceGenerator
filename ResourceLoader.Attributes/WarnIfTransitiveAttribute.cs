using System;

namespace ResourceLoader.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class WarnIfTransitiveAttribute : Attribute { }
}
