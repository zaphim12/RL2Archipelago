using System.Text;

namespace RL2Archipelago;

/// <summary>
/// FNV-1a, used wherever the mod needs a small stable hash. Chosen over <c>string.GetHashCode</c>
/// because that is not guaranteed stable between runtimes or runs, and over
/// <c>UnityEngine.Random</c> because anything derived from a seed has to be reproducible on a later
/// launch (and because drawing from Unity's shared generator perturbs everything else using it in
/// the same frame).
/// </summary>
internal static class Hashing
{
    private const uint OffsetBasis = 2166136261;
    private const uint Prime = 16777619;

    internal static uint Fnv1a32(string value)
    {
        uint hash = OffsetBasis;
        foreach (byte b in Encoding.UTF8.GetBytes(value ?? string.Empty))
        {
            hash ^= b;
            hash *= Prime;
        }
        return hash;
    }

    internal static string Fnv1a32Hex(string value) => Fnv1a32(value).ToString("x8");
}
