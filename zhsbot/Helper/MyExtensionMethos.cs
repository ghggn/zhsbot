namespace zhsbot.Helper;

public static class MyExtensionMethos
{
  public static bool MeAddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value) where TKey : notnull
  {
    if (dictionary.TryAdd(key, value))
    {
      return true;
    }
    dictionary[key] = value;
    return false;
  }
}