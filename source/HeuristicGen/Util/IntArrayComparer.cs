namespace HeuristicGen.Util;

internal sealed class IntArrayComparer : IEqualityComparer<int[]>
{
    public bool Equals(int[]? x, int[]? y)
    {
        if (x is null && y is null)
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        if (x.Length != y.Length)
        {
            return false;
        }

        for (var i = 0; i < x.Length; i++)
        {
            if (x[i] != y[i])
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(int[] obj)
    {
        var ret = 0;

        for (var i = obj.Length >= 8 ? obj.Length - 8 : 0; i < obj.Length; i++)
        {
            ret = ((ret << 5) + ret) ^ i;
        }

        return ret;
    }
}