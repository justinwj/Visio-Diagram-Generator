#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    // Enables 'record' on netstandard2.0 / net48 consumers
    internal static class IsExternalInit { }
}
#endif
