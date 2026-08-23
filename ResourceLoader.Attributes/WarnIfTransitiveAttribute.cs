using System;

namespace ResourceLoader.Attributes
{
    /// <summary>
    /// Marks a loader class so that a warning is emitted when it is pulled in transitively
    /// through a bundle-of-bundles rather than being directly registered or included in a
    /// directly applied bundle.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class WarnIfTransitiveAttribute : Attribute { }
}
