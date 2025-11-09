namespace HeuristicGen.Util;

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

    public static int[] Argsort<T>(this IList<T> items, Comparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        var indices = Enumerable.Range(0, items.Count).ToArray();
        Array.Sort(indices, (a, b) => comparer.Compare(items[a], items[b]));
        return indices;
    }

    public enum RankingMethod
    {
        Average,
        Dense,
        Max,
        Min,
        Ordinal
    }

    public static double[] RankData<T>(this IList<T> items, RankingMethod method = RankingMethod.Average,
        Comparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        var indices = Argsort(items, comparer);
        var ranks = new double[items.Count];
        var i = 0;
        var r = 1.0;
        while (i < indices.Length)
        {
            ranks[indices[i]] = r;
            var j = i;
            while (j < indices.Length - 1 && comparer.Compare(items[indices[j]], items[indices[j + 1]]) == 0)
            {
                j++;
            }

            if (i < j)
            {
                var newRank = method switch
                {
                    RankingMethod.Average => r + (j - i) / 2.0,
                    RankingMethod.Dense => r,
                    RankingMethod.Max => r + j - i,
                    RankingMethod.Min => r,
                    _ => r
                };
                for (var k = i; k <= j; k++)
                {
                    if (method == RankingMethod.Ordinal)
                    {
                        ranks[indices[k]] = newRank++;
                    }
                    else
                    {
                        ranks[indices[k]] = newRank;
                    }
                }

                if (method != RankingMethod.Dense)
                {
                    r += j - i;
                }

                i = j;
            }

            r++;
            i++;
        }

        return ranks;
    }
}