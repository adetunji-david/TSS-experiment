namespace HeuristicGen.Rng
{
    public class SeedSource
    {
        private const int DefaultPoolSize = 4;
        private const uint InitA = 0x43b0d7e5;
        private const uint MultA = 0x931e8875;
        private const uint InitB = 0x8b51f9dd;
        private const uint MultB = 0x58f38ded;
        private const uint MixMultL = 0xca01f9dd;
        private const uint MixMultR = 0x4973f715;
        private const int Xshift = 16;


        private readonly uint[] _pool;
        private uint _hashConstant = InitA;

        public SeedSource(int poolSize, params uint[] entropies)
        {
            if (poolSize < DefaultPoolSize)
                throw new ArgumentException($"The size of the entropy pool should be at least {DefaultPoolSize}");
            _pool = new uint[poolSize];
            MixEntropies(entropies);
        }

        public void Fill(Span<byte> destination)
        {
            var poolIndex = 0;
            var dstIndex = 0;
            var localHashConstant = InitB;
            while (dstIndex < destination.Length)
            {
                var dataVal = _pool[poolIndex];
                dataVal ^= localHashConstant;
                localHashConstant *= MultB;
                dataVal *= localHashConstant;
                dataVal ^= dataVal >> Xshift;
                var mask = 0xFF_00_00_00u;
                var rs = 24;
                while (mask > 0 && dstIndex < destination.Length)
                {
                    destination[dstIndex++] = (byte)((dataVal & mask) >> rs);
                    mask /= 256u;
                    rs -= 8;
                }

                poolIndex++;
                poolIndex %= _pool.Length;
            }
        }

        public static SeedSource Create()
        {
            unchecked
            {
                var threadId = (uint)Environment.CurrentManagedThreadId;
                var time = (uint)Environment.TickCount;
                uint memoryLocation;
                unsafe
                {
                    int x;
                    memoryLocation = (uint)&x;
                }

                return new SeedSource(4, threadId, time, memoryLocation);
            }
        }

        private void MixEntropies(uint[] entropies)
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                if (i < entropies.Length)
                    _pool[i] = Hash(entropies[i]);
                else
                    _pool[i] = Hash(0);
            }

            for (var i = 0; i < _pool.Length; i++)
            for (var j = 0; j < _pool.Length; j++)
            {
                if (i != j)
                    _pool[j] = Mix(_pool[j], Hash(_pool[i]));
            }

            for (var i = _pool.Length; i < entropies.Length; i++)
            for (var j = 0; j < _pool.Length; j++)
                _pool[j] = Mix(_pool[j], Hash(entropies[i]));
        }

        private uint Hash(uint value)
        {
            unchecked
            {
                value ^= _hashConstant;
                _hashConstant *= MultA;
                value *= _hashConstant;
                value ^= value >> Xshift;
                return value;
            }
        }

        private static uint Mix(uint x, uint y)
        {
            unchecked
            {
                var result = MixMultL * x - MixMultR * y;
                result ^= result >> Xshift;
                return result;
            }
        }
    }
}