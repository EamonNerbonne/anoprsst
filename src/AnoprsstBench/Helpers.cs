using System;
using System.Collections.Generic;
using System.Globalization;
using Anoprsst;
using IncrementalMeanVarianceAccumulator;

namespace AnoprsstBench
{
    public static class Helpers
    {
        public static string MSE(MeanVarianceAccumulator acc)
            => MSE(acc.Mean, StdErr(acc));

        public static double CostScalingEstimate(double len)
            => len * Math.Sqrt(len + 40.0) + 15.0;

        public static double StdErr(MeanVarianceAccumulator acc)
            => acc.SampleStandardDeviation / Math.Sqrt(acc.WeightSum);

        public static string MSE(double mean, double stderr)
        {
            var significantDigits = Math.Log10(Math.Abs(mean / stderr));
            var digitsToShow = Math.Max(2, (int)(significantDigits + 2.5));

            if (Math.Pow(10, digitsToShow) <= mean && Math.Pow(10, digitsToShow + 2) > mean) {
                return mean.ToString("f0") + "~" + stderr.ToString("f0");
            }
            var fmtString = "g" + digitsToShow;
            return mean.ToString(fmtString) + "~" + stderr.ToString("g2");
        }

        public static ulong[] RandomizeUInt64(int size)
        {
            var arr = new ulong[size];
            var r = new Random(37);
            for (var j = 0; j < arr.Length; j++) {
                arr[j] = ((ulong)(uint)r.Next() << 32) + (uint)r.Next();
            }
            return arr;
        }

        public static (int, long, DateTime, string, Guid) MapToBigStruct(ulong data)
            => ((int)(data >> 48), (long)(data - (data >> 48 << 48)), new DateTime(2000, 1, 1) + TimeSpan.FromSeconds((int)data), "test", default(Guid));

        public static (int, int, int) MapToSmallStruct(ulong data)
            => ((int)(data >> 32), (int)(data - (data >> 32 << 32)), (int)(data * 13));

        public static int MapToInt32(ulong data)
            => (int)(data >> 32);

        public static ulong MapToUInt64(ulong data)
            => data;

        public static uint MapToUInt32(ulong data)
            => (uint)(data >> 32);

        public static string MapToString(ulong data)
            => MapToUInt32(data).ToString("x8", CultureInfo.InvariantCulture);

        public static SampleClass MapToSampleClass(ulong data)
            => new SampleClass { Value = (int)(data >> 32) };

        public static double MapToDouble(ulong data)
            => (long)data / (double)(1L << 31);

        public static float MapToFloat(ulong data)
            => (long)data / (float)(1L << 31);
    }

    public struct OrderComparer<T, TOrder> : IComparer<T>
        where TOrder : IOrdering<T>
    {
        readonly TOrder underlying;

        public OrderComparer(TOrder underlying)
            => this.underlying = underlying;

        public int Compare(T x, T y)
            => underlying.LessThan(x, y) ? -1
                : underlying.LessThan(y, x) ? 1
                : 0;
    }
}
