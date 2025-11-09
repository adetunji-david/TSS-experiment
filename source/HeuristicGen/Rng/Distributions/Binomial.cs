namespace HeuristicGen.Rng.Distributions
{
    public sealed class Binomial
    {
        public long NumTrials { get; }
        public double SuccessProbability { get; }
        private readonly bool _useInversion;
        private readonly long _m;

        private readonly long _n;
        private readonly double _bound;
        private readonly double _nlogq;
        private readonly double _q;
        private readonly double _p1;
        private readonly double _xm;
        private readonly double _xl;
        private readonly double _xr;
        private readonly double _c;
        private readonly double _laml;
        private readonly double _lamr;
        private readonly double _p2;
        private readonly double _p3;
        private readonly double _p4;
        private readonly double _p;

        public Binomial(long numTrials, double successProbability)
        {
            if (successProbability is < 0 or > 1)
            {
                throw new ArgumentException("successProbability must be between 0 and 1.");
            }

            if (numTrials < 0)
            {
                throw new ArgumentException("numTrials must be non-negative.");
            }

            NumTrials = numTrials;
            SuccessProbability = successProbability;
            _p = Math.Min(successProbability, 1.0 - successProbability);
            _n = numTrials;
            _q = 1.0 - _p;
            _useInversion = _p * _n <= 30.0;
            if (_useInversion)
            {
                _nlogq = Math.Exp(_n * Math.Log(_q));
                var np = _n * _p;
                _bound = (long)Math.Min(_n, np + 10.0 * Math.Sqrt(np * _q + 1));
            }
            else
            {
                var fm = _n * _p + _p;
                _m = (long)Math.Floor(fm);
                _p1 = Math.Floor(2.195 * Math.Sqrt(_n * _p * _q) - 4.6 * _q) + 0.5;
                _xm = _m + 0.5;
                _xl = _xm - _p1;
                _xr = _xm + _p1;
                _c = 0.134 + 20.5 / (15.3 + _m);
                var a = (fm - _xl) / (fm - _xl * _p);
                _laml = a * (1.0 + a / 2.0);
                a = (_xr - fm) / (_xr * _q);
                _lamr = a * (1.0 + a / 2.0);
                _p2 = _p1 * (1.0 + 2.0 * _c);
                _p3 = _p2 + _c / _laml;
                _p4 = _p3 + _c / _lamr;
            }
        }

        private long RandomBinomialBtpe(Pcg64 random)
        {
            long y, i;
            Step10:
            var nrq = _n * _p * _q;
            var u = random.NextDouble() * _p4;
            var v = random.NextDouble();
            if (u > _p1)
                goto Step20;
            y = (long)Math.Floor(_xm - _p1 * v + u);
            goto Step60;

            Step20:
            if (u > _p2)
                goto Step30;
            var x = _xl + (u - _p1) / _c;
            v = v * _c + 1.0 - Math.Abs(_m - x + 0.5) / _p1;
            if (v > 1.0)
                goto Step10;
            y = (long)Math.Floor(x);
            goto Step50;

            Step30:
            if (u > _p3)
                goto Step40;
            y = (long)Math.Floor(_xl + Math.Log(v) / _laml);
            if (y < 0 || v == 0.0)
                goto Step10;
            v = v * (u - _p2) * _laml;
            goto Step50;

            Step40:
            y = (long)Math.Floor(_xr - Math.Log(v) / _lamr);
            if (y > _n || v == 0.0)
                goto Step10;
            v = v * (u - _p3) * _lamr;

            Step50:
            var k = Math.Abs(y - _m);
            if (k > 20 && k < nrq / 2.0 - 1)
                goto Step52;

            var s = _p / _q;
            var a = s * (_n + 1);
            var f = 1.0;
            if (_m < y)
            {
                for (i = _m + 1; i <= y; i++)
                {
                    f *= a / i - s;
                }
            }
            else if (_m > y)
            {
                for (i = y + 1; i <= _m; i++)
                {
                    f /= a / i - s;
                }
            }

            if (v > f)
                goto Step10;
            goto Step60;

            Step52:
            var rho = k / nrq * ((k * (k / 3.0 + 0.625) + 0.16666666666666666) / nrq + 0.5);
            var t = -k * k / (2 * nrq);
            var logV = Math.Log(v);
            if (logV < t - rho)
                goto Step60;
            if (logV > t + rho)
                goto Step10;

            double x1 = y + 1;
            double f1 = _m + 1;
            double z = _n + 1 - _m;
            double w = _n - y + 1;
            var x2 = x1 * x1;
            var f2 = f1 * f1;
            var z2 = z * z;
            var w2 = w * w;
            if (logV > _xm * Math.Log(f1 / x1) + (_n - _m + 0.5) * Math.Log(z / w) +
                (y - _m) * Math.Log(w * _p / (x1 * _q)) +
                (13680.0 - (462.0 - (132.0 - (99.0 - 140.0 / f2) / f2) / f2) / f2) / f1 /
                166320.0 +
                (13680.0 - (462.0 - (132.0 - (99.0 - 140.0 / z2) / z2) / z2) / z2) / z /
                166320.0 +
                (13680.0 - (462.0 - (132.0 - (99.0 - 140.0 / x2) / x2) / x2) / x2) / x1 /
                166320.0 +
                (13680.0 - (462.0 - (132.0 - (99.0 - 140.0 / w2) / w2) / w2) / w2) / w /
                166320.0)
            {
                goto Step10;
            }

            Step60:
            if (_p > 0.5)
            {
                y = _n - y;
            }

            return y;
        }

        private long RandomBinomialInversion(Pcg64 random)
        {
            long x = 0;
            var px = _nlogq;
            var u = random.NextDouble();
            while (u > px)
            {
                x++;
                if (x > _bound)
                {
                    x = 0;
                    px = _nlogq;
                    u = random.NextDouble();
                }
                else
                {
                    u -= px;
                    px = (_n - x + 1) * _p * px / (x * _q);
                }
            }

            return x;
        }

        public long Sample(Pcg64 random)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (SuccessProbability == 1.0) return _n;
            if (_n == 0 || SuccessProbability == 0.0) return 0;
            var x = _useInversion ? RandomBinomialInversion(random) : RandomBinomialBtpe(random);
            return SuccessProbability <= 0.5 ? x : _n - x;
        }

        public void Fill(Pcg64 random, Span<long> buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Sample(random);
            }
        }
    }
}