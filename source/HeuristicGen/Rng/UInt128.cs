namespace HeuristicGen.Rng;

public readonly struct UInt128
{
    public readonly ulong High;
    public readonly ulong Low;

    public UInt128(ulong high, ulong low)
    {
        High = high;
        Low = low;
    }

    public static UInt128 operator +(UInt128 a, UInt128 b)
    {
        unchecked
        {
            var newLow = a.Low + b.Low;
            var overflow = newLow < b.Low ? 1ul : 0ul;
            return new UInt128(a.High + b.High + overflow, newLow);
        }
    }

    public static UInt128 operator *(UInt128 a, UInt128 b)
    {
        unchecked
        {
            var h1 = a.High * b.Low + a.Low * b.High;
            UMul128(a.Low, b.Low, out var high, out var low);
            high += h1;
            return new UInt128(high, low);
        }
    }

    public static UInt128 operator *(UInt128 a, ulong b)
    {
        unchecked
        {
            var h1 = a.High * b;
            UMul128(a.Low, b, out var high, out var low);
            high += h1;
            return new UInt128(high, low);
        }
    }

    private static void UMul128(ulong x, ulong y, out ulong high, out ulong low)
    {
        unchecked
        {
            low = x * y;
            var x0 = x & 0xFFFFFFFFul;
            var x1 = x >> 32;
            var y0 = y & 0xFFFFFFFFul;
            var y1 = y >> 32;
            var w0 = x0 * y0;
            var t = x1 * y0 + (w0 >> 32);
            var w1 = t & 0xFFFFFFFFul;
            var w2 = t >> 32;
            w1 += x0 * y1;
            high = x1 * y1 + w2 + (w1 >> 32);
        }
    }
}