using System.Collections.Generic;

public static class PlayerInventory
{
    private static readonly HashSet<E_KeyType> _keys = new();
    public static bool Has(E_KeyType k) => _keys.Contains(k);
    public static void Add(E_KeyType k) => _keys.Add(k);
}
