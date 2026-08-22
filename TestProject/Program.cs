using ResourceLoader.Attributes;
using ResourceLoader.Defaults;

namespace TestProject
{
    [UseDefaultLoaders]
    [ResourceFolder("Resources", nameof(_resources))]
    internal partial class Program
    {
        string _resources = "shit";
        
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
        }
    }
}
