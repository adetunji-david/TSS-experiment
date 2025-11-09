// ReSharper disable CompareOfFloatsByEqualityOperator

namespace HeuristicGen.Rng.Distributions
{
    public sealed class TruncatedZipf
    {
        private readonly double _normalizationConstant;
        private readonly double _lbProposalCdf;
        public double Exponent { get; }
        public int LowerBound { get; }
        public int UpperBound { get; }

        public TruncatedZipf(double exponent, int lowerBound, int upperBound)
        {
            if (exponent < 0)
            {
                throw new ArgumentException("exponent must be non-negative.");
            }

            if (lowerBound < 1)
            {
                throw new ArgumentException("Lower bound must be an integer at least 1.");
            }

            if (lowerBound > upperBound)
            {
                throw new ArgumentException("Upper bound must be greater than the lower bound.");
            }

            if (Math.Abs(1.0 - exponent) < 1e-8)
            {
                exponent = 1.0;
            }

            Exponent = exponent;
            LowerBound = lowerBound;
            UpperBound = upperBound;

            if (exponent == 1.0)
            {
                _normalizationConstant = 1.0 + Math.Log(upperBound);
            }
            else
            {
                _normalizationConstant =
                    (Math.Pow(upperBound, 1.0 - Exponent) - Exponent) / (1.0 - Exponent);
            }

            _lbProposalCdf = ProposalCdf(lowerBound - 1);
        }


        public int Sample(Pcg64 random)
        {
            while (true)
            {
                var u = 1.0 - random.NextDouble(); // 0 < u <= 1
                var y = ProposalInverseCdf(_lbProposalCdf + u * (1.0 - _lbProposalCdf));
                var x = (int)Math.Ceiling(y);
                var proposalUnnormalizedPdf = ProposalUnnormalizedPdf(y);
                var zipfUnnormalizedPmf = Math.Pow(x, -Exponent);
                var v = random.NextDouble();
                if (v * proposalUnnormalizedPdf <= zipfUnnormalizedPmf)
                {
                    // clip as a safe-guard against floating point errors
                    return Math.Min(Math.Max(x, LowerBound), UpperBound);
                }
            }
        }

        public void Fill(Pcg64 random, Span<int> buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Sample(random);
            }
        }

        private double ProposalUnnormalizedPdf(double y)
        {
            return y <= 1 ? 1.0 : Math.Pow(y, -Exponent);
        }

        private double ProposalCdf(double y)
        {
            if (y <= 1.0)
            {
                return y / _normalizationConstant;
            }

            if (Exponent == 1.0)
            {
                return (1.0 + Math.Log(y)) / _normalizationConstant;
            }
            else
            {
                var denom = _normalizationConstant * (1.0 - Exponent);
                return (Math.Pow(y, 1.0 - Exponent) - Exponent) / denom;
            }
        }

        private double ProposalInverseCdf(double u)
        {
            var y = u * _normalizationConstant;
            if (y <= 1.0)
            {
                return y;
            }

            if (Exponent == 1.0)
            {
                return Math.Exp(y - 1);
            }
            else
            {
                var t = 1.0 / (1.0 - Exponent);
                return Math.Pow(y * (1.0 - Exponent) + Exponent, t);
            }
        }
    }
}