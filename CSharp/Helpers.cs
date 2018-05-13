using System;
using System.Runtime.CompilerServices;
using IncrementalMeanVarianceAccumulator;

namespace SortAlgoBench {
    static class Helpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Swap<T>(this T[] arr, int a, int b) { (arr[a], arr[b]) = (arr[b], arr[a]); }

        public static int ProcScale()
        {
            var splitIters = 4;
            var threads = Environment.ProcessorCount;
            while (threads > 0) {
                threads = threads >> 1;
                splitIters++;
            }

            return splitIters;
        }

        public static string MSE(MeanVarianceAccumulator acc)
            => MSE(acc.Mean, StdErr(acc));

        static double StdErr(MeanVarianceAccumulator acc)
            => acc.SampleStandardDeviation / Math.Sqrt(acc.WeightSum);

        public static string MSE(double mean, double stderr)
        {
            var significantDigits = Math.Log10(Math.Abs(mean / stderr));
            var digitsToShow = Math.Max(2, (int)(significantDigits + 1.9));
            var fmtString = "g" + digitsToShow;
            return mean.ToString(fmtString) + "~" + stderr.ToString("g2");
        }

        public static ulong[] RandomizeUInt64()
        {
            var arr = new ulong[SortAlgoBenchProgram.MaxArraySize];
            var r = new Random(37);
            for (var j = 0; j < arr.Length; j++)
                arr[j] = (((ulong)(uint)r.Next() << 32) + (uint)r.Next());
            return arr;
        }

        public static (int, long, DateTime, string)[] MapToBigStruct(ulong[] data)
        {
            var arr = new (int, long, DateTime, string)[data.Length];
            for (var j = 0; j < arr.Length; j++)
                arr[j] = ((int)(data[j] >>48), (long)(data[j] -(data[j] >>48<<48)), new DateTime(2000,1,1)+TimeSpan.FromSeconds((int)data[j]), data[j].ToString("x"));
            return arr;
        }

        public static int[] MapToInt32(ulong[] data)
        {
            var arr = new int[data.Length];
            for (var j = 0; j < arr.Length; j++)
                arr[j] = (int)(data[j]>>32);
            return arr;
        }

        public static SampleClass[] MapToSampleClass(ulong[]data)
        {
            var arr = new SampleClass[data.Length];
            for (var j = 0; j < arr.Length; j++)
                arr[j] = new SampleClass { Value = (int)(data[j]>>32) };
            return arr;
        }

        public static uint[] MapToUInt32(ulong[]data)
        {
            var arr = new uint[data.Length];
            for (var j = 0; j < arr.Length; j++)
                arr[j] = (uint)(data[j]>>32);
            return arr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BoundsCheck<T>(T[] array, int firstIdx, int lastIdx)
        {
            if (0 > firstIdx || firstIdx > lastIdx || lastIdx > array.Length)
            {
                ThrowIndexOutOfRange(array, firstIdx, lastIdx);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowIndexOutOfRange<T>(T[] array, int firstIdx, int lastIdx)
        {
            throw new IndexOutOfRangeException($"Attempted to sort [{firstIdx}, {lastIdx}), which not entirely within bounds of [0, {array.Length})");
        }
    }
}