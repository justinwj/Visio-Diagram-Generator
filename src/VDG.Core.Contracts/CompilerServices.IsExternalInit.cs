// Allows 'record'/'init' syntax on targets older than .NET 5
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
