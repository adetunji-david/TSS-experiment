// ReSharper disable CompareOfFloatsByEqualityOperator

namespace HeuristicGen.Rng.Distributions
{
    public sealed class TruncatedPowerLaw
    {
        private readonly double _invt;
        private readonly double _lbe;

        private readonly double _z;

        public double Exponent { get; }
        public int LowerBound { get; }
        public int UpperBound { get; }

        public TruncatedPowerLaw(double exponent, int lowerBound, int upperBound)
        {
            if (exponent >= -1)
            {
                throw new ArgumentException("exponent must be less than -1.");
            }

            if (lowerBound < 1)
            {
                throw new ArgumentException("lowerBound must be an integer at least 1.");
            }

            if (lowerBound > upperBound)
            {
                throw new ArgumentException("upperBound must be greater than the lower bound.");
            }

            Exponent = exponent;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            _invt = 1.0 / (1.0 + exponent);
            _lbe = Math.Pow(lowerBound, 1.0 + exponent);
            _z = Math.Pow(upperBound, 1.0 + exponent) - _lbe;
        }

        public double Sample(Pcg64 random)
        {
            var u = random.NextDouble();
            var x = Math.Pow(u * _z + _lbe, _invt);
            return Math.Min(Math.Max(x, LowerBound), UpperBound);
        }

        public void Fill(Pcg64 random, Span<double> buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Sample(random);
            }
        }
    }
}