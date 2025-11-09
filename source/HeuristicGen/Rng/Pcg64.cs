using System.Runtime.CompilerServices;

namespace HeuristicGen.Rng;

/// <summary>
/// Pcg64 (Permuted Congruential Generator) is a C# implementation of
/// PCG XSL RR 128/64 generator presented in "PCG: A Family of Simple Fast Space-Efficient
/// Statistically Good Algorithms for Random Number Generation" by Melissa E. O'Neill.
/// </summary>
public sealed class Pcg64 : Random
{
    private const double Reciprocal = 1.0 / 9007199254740992.0; // 2^-53
    private static readonly UInt128 DefaultMultiplier = new(2549297995355413924ul, 4865540595714422341ul);
    private UInt128 _state;
    private readonly UInt128 _increment;
    private uint _unusedValue;
    private bool _hasUnusedValue;

    public Pcg64(UInt128 initState, UInt128 initSeq)
    {
        _state = new UInt128(0ul, 0ul);
        _increment = new UInt128((initSeq.High << 1) | (initSeq.Low >> 63), (initSeq.Low << 1) | 1);
        Transition();
        _state += initState;
        Transition();
    }

    public static Pcg64 FromSeedBuffer(Span<byte> seedBuffer)
    {
        if (seedBuffer.Length < 32)
        {
            throw new ArgumentException("seed buffer does not contain 32 byte values");
        }

        var initStateHigh = BitConverter.ToUInt64(seedBuffer[..8]);
        var initStateLow = BitConverter.ToUInt64(seedBuffer[8..16]);
        var initSeqHigh = BitConverter.ToUInt64(seedBuffer[16..24]);
        var initSeqLow = BitConverter.ToUInt64(seedBuffer[24..32]);
        var initState = new UInt128(initStateHigh, initStateLow);
        var initSeq = new UInt128(initSeqHigh, initSeqLow);
        return new Pcg64(initState, initSeq);
    }

    public static Pcg64 Create(params uint[] seeds)
    {
        Span<byte> seedBuffer = stackalloc byte[32];
        if (seeds.Length == 0)
        {
            SeedSource.Create().Fill(seedBuffer);
        }
        else
        {
            new SeedSource(4, seeds).Fill(seedBuffer);
        }

        return FromSeedBuffer(seedBuffer);
    }

    public uint NextUInt32()
    {
        if (_hasUnusedValue)
        {
            _hasUnusedValue = false;
            return _unusedValue;
        }

        Transition();
        var nextLongValue = Output();
        _hasUnusedValue = true;
        _unusedValue = (uint)(nextLongValue >> 32);
        return (uint)(nextLongValue & 0x_FFFF_FFFF);
    }

    public ulong NextUInt64()
    {
        _hasUnusedValue = false;
        Transition();
        return Output();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong Output()
        => RotateRight(_state.High ^ _state.Low, (int)(_state.High >> 58));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Transition()
    {
        _state = _state * DefaultMultiplier + _increment;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong RotateRight(ulong value, int count)
        => (value >> count) | (value << (64 - count));

    public override double NextDouble()
        => (NextUInt64() >> 11) * Reciprocal;

    public override int Next()
    {
        int x;
        do
        {
            x = (int)(NextUInt32() >> 1);
        } while (x == int.MaxValue);

        return x;
    }

    public override int Next(int maxExclusive)
    {
        unchecked
        {
            switch (maxExclusive)
            {
                case 0:
                    return 0;
                case < 0:
                    throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            // see https://www.pcg-random.org/posts/bounded-rands.html, Lemire's method
            var range = (uint)maxExclusive;
            var x = (ulong)NextUInt32() * range;
            var leftOver = (uint)x; // leftOver = x % 2^32
            if (leftOver >= range)
            {
                return (int)(x >> 32);
            }

            var threshold = (uint)(-range % range); // threshold = 2^32 % range
            while (leftOver < threshold)
            {
                x = (ulong)NextUInt32() * range;
                leftOver = (uint)x;
            }

            return (int)(x >> 32);
        }
    }

    public override int Next(int minInclusive, int maxExclusive)
    {
        unchecked
        {
            if (maxExclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(minInclusive));
            }

            var range = (ulong)((long)maxExclusive - minInclusive);
            switch (range)
            {
                case 0 or 1:
                    return minInclusive;
                case < uint.MaxValue:
                {
                    var rangeAsUint = (uint)range;
                    var x = (ulong)NextUInt32() * rangeAsUint;
                    var leftOver = (uint)x;
                    if (leftOver >= range)
                    {
                        return (int)(x >> 32) + minInclusive;
                    }

                    var threshold = (uint)(-rangeAsUint % rangeAsUint);
                    while (leftOver < threshold)
                    {
                        x = (ulong)NextUInt32() * rangeAsUint;
                        leftOver = (uint)x;
                    }

                    return (int)(x >> 32) + minInclusive;
                }
                default:
                {
                    // debiased modulo, OpenBSD's Method
                    // threshold = 2^64 % range.
                    // Can't use trick in previous case as we can't negate ulongs.
                    // Addition order is important.
                    var threshold = (ulong.MaxValue - range + 1) % range;
                    var x = NextUInt64();
                    while (x < threshold)
                    {
                        x = NextUInt64();
                    }

                    return (int)((long)(x % range) + minInclusive);
                }
            }
        }
    }

    public override void NextBytes(Span<byte> buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(NextUInt32() % 256);
        }
    }
}