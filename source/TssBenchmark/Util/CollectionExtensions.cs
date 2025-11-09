namespace TssBenchmark.Util;

public static class CollectionExtensions
{
    public static void AddMany<T>(this HashSet<T> hashSet, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            hashSet.Add(item);
        }
    }

    public static void RemoveMany<T>(this HashSet<T> hashSet, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            hashSet.Remove(item);
        }
    }

    public static T RemoveFirst<T>(this HashSet<T> hashSet)
    {
        var item = hashSet.First();
        hashSet.Remove(item);
        return item;
    }
}