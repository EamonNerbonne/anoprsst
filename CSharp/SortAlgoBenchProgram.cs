using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using IncrementalMeanVarianceAccumulator;
using ExpressionToCodeLib;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
namespace SortAlgoBench {
    static class SortAlgoBenchProgram {
        public static readonly int ParallelSplitScale = Helpers.ProcScale();

        static void Main() {
            const int quality = 100;
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
            var small = BenchSize(1 << 7 , quality);
            Console.WriteLine();
            var med = BenchSize(1 << 10 , quality);
            Console.WriteLine();
            var large = BenchSize(1 << 14, quality);
            Console.WriteLine();
            var xlarge = BenchSize(1 << 19,  quality);
            var all = new[] { small, med, large, xlarge, }.SelectMany(x => x).ToArray();

            Console.WriteLine();
            foreach (var byType in all.GroupBy(o => o.type))
                Console.WriteLine($"{byType.Key.ToCSharpFriendlyTypeName()}: {byType.Average(o => o.nsPerArrayItem):f1}ns/item");

            Console.WriteLine();
            foreach (var byMethod in all.GroupBy(o => o.method))
                Console.WriteLine($"{byMethod.Key}: {byMethod.Average(o => o.nsPerArrayItem):f1}ns/item");

            Console.WriteLine();
            Console.WriteLine($"OVERALL: {all.Average(o => o.nsPerArrayItem):f1}ns/item");
        }

        private static (string method, Type type, double nsPerArrayItem, double nsStdErr)[] BenchSize(int targetSize, int quality) {
            var backingArraySize = targetSize << 4;
            var iterations = 3 + (int)(0.5+30_000_000.0*quality/targetSize/Math.Log(targetSize));

            var data = Helpers.RandomizeUInt64(backingArraySize);
            
            return new[]{
                DoubleOrderingAlgorithms.BencherFor(Helpers.MapToDouble(data), iterations * 3 / 2).BenchVariousAlgos(),
                Int32OrderingAlgorithms.BencherFor(Helpers.MapToInt32(data), iterations * 3 / 2).BenchVariousAlgos(),
                SmallStructOrderingAlgorithms.BencherFor(Helpers.MapToSmallStruct(data), iterations).BenchVariousAlgos(),
                SampleClassOrderingAlgorithms.BencherFor(Helpers.MapToSampleClass(data), iterations * 2 / 3).BenchVariousAlgos(),
                UInt64OrderingAlgorithms.BencherFor(data, iterations).BenchVariousAlgos(),
                BigStructOrderingAlgorithms.BencherFor(Helpers.MapToBigStruct(data), iterations / 2).BenchVariousAlgos(),
                UInt32OrderingAlgorithms.BencherFor(Helpers.MapToUInt32(data), iterations * 3 / 2).BenchVariousAlgos(),
                ComparableOrderingAlgorithms<int>.BencherFor(Helpers.MapToInt32(data), iterations * 3 / 2).BenchVariousAlgos(),
            }.SelectMany(r => r).ToArray();
        }
    }

    public sealed class SortAlgorithmBench<T, TOrder>
        where TOrder : struct, IOrdering<T> {
        public IEnumerable<(string method, Type type, double nsPerArrayItem, double nsStdErr)> BenchVariousAlgos() {
            var meanLen = SubArrays().Average(o=>o.len);
            Console.WriteLine($"Benchmarking {Iterations} {typeof(T).ToCSharpFriendlyTypeName()}[{meanLen:f1}]");

            yield return BenchSort(SystemArraySort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.DualPivotQuickSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.QuickSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.ParallelQuickSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.BottomUpMergeSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.TopDownMergeSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.AltTopDownMergeSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.AltTopDownMergeSort2);

            Console.WriteLine();
        }

        static void ArraySort_Primitive(T[] arr, int len) { Array.Sort(arr, 0, len); }
        static void ArraySort_OrderComparer(T[] arr, int len) { Array.Sort(arr, 0, len, Helpers.ComparerFor<T, TOrder>()); }

        static readonly Action<T[], int> SystemArraySort = typeof(T).IsPrimitive ? (Action<T[], int>)ArraySort_Primitive : ArraySort_OrderComparer;

        public SortAlgorithmBench(T[] sourceData, int iterations) {
            SourceData = sourceData;
            Iterations = iterations;
            workspace = new T[sourceData.Length >> 3];
        }

        readonly T[] workspace;
        readonly T[] SourceData;
        readonly int Iterations;

        IEnumerable<(int offset, int len)> SubArrays() {
            var random = new Random(42);
            for (var k = 0; k < Iterations; k++) {
                var len = random.Next(workspace.Length + 1);
                var offset = random.Next(SourceData.Length - len + 1);
                yield return (offset, len);
            }
        }

        void RefreshData((int offset, int len) subsegment) {
            Array.Copy(SourceData, subsegment.offset, workspace, 0, subsegment.len);
        }

        public (string method, Type type, double nsPerArrayItem, double nsStdErr) BenchSort(Action<T[], int> action) {
            var method = action.Method.Name;
            var sizes = new List<int>();
            var milliseconds = new List<double>();
            var random = new Random(42);
            var sw = new Stopwatch();
            var swOverhead = Stopwatch.StartNew();
            foreach (var subsegment in SubArrays()) {
                RefreshData(subsegment);
                var len = subsegment.len;
                long checkSum = 0;
                for (var j = 0; j < len; j++) {
                    var l = workspace[j];
                    checkSum = checkSum + l.GetHashCode();
                }

                if (false && len > 50) {
                    var sorted = true;
                    for (var j = 1; j < len; j++)
                        if (!default(TOrder).LessThan(workspace[j], workspace[j - 1]))
                            sorted = false;
                    if (sorted) {
                        Console.WriteLine("Already sorted??");
                        break;
                    }
                }

                sw.Restart();
                action(workspace, len);
                sw.Stop();

                for (var j = 0; j < len; j++) {
                    var l = workspace[j];
                    checkSum = checkSum - l.GetHashCode();
                }
                if (checkSum != 0)
                    Console.WriteLine(method + " has differing elements before and after sort");
                for (var j = 1; j < len; j++)
                    if (default(TOrder).LessThan(workspace[j], workspace[j - 1])) {
                        Console.WriteLine(method + " did not sort.");
                        break;
                    }


                milliseconds.Add(sw.Elapsed.TotalMilliseconds);
                sizes.Add(len);
            }


            milliseconds.Sort();
            var meanLen = sizes.Average();
            var msDistrib = MeanVarianceAccumulator.FromSequence(milliseconds.Skip(milliseconds.Count >> 5).Take(milliseconds.Count - (milliseconds.Count >> 5 <<1)));
            var nsPerItem = msDistrib.Mean / meanLen * 1000_000;
            var nsStdErr = Helpers.StdErr(msDistrib) / meanLen * 1000_000;
            var medianNsPerItem = (milliseconds[milliseconds.Count >>1] + milliseconds[milliseconds.Count+1 >>1]) / 2.0 / meanLen * 1000_000;
            Console.WriteLine($"{method.PadLeft(23)}: mean {Helpers.MSE(nsPerItem,nsStdErr).PadRight(11)} ns/item; median {medianNsPerItem:f1}; overhead: {100*(1- milliseconds.Sum()/swOverhead.Elapsed.TotalMilliseconds):f1}%");
            return (action.Method.Name.StartsWith("ArraySort_") ? "ArraySort" : method, typeof(T), nsPerItem, nsStdErr);
        }
    }

    public abstract class ComparableOrderingAlgorithms<T> : OrderedAlgorithms<T, ComparableOrderingAlgorithms<T>.ComparableOrdering>
        where T : IComparable<T> {
        public struct ComparableOrdering : IOrdering<T> {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(T a, T b) => a.CompareTo(b) < 0;
        }
    }

    public abstract class UInt64OrderingAlgorithms : OrderedAlgorithms<ulong, UInt64OrderingAlgorithms.UInt64Ordering> {
        public struct UInt64Ordering : IOrdering<ulong> {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(ulong a, ulong b) => a < b;
        }
    }

    public abstract class UInt32OrderingAlgorithms : OrderedAlgorithms<uint, UInt32OrderingAlgorithms.UInt32Order> {
        public struct UInt32Order : IOrdering<uint> {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(uint a, uint b) => a < b;
        }
    }

    public abstract class Int32OrderingAlgorithms : OrderedAlgorithms<int, Int32OrderingAlgorithms.Int32Order> {
        public struct Int32Order : IOrdering<int> {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(int a, int b) => a < b;
        }
    }

    public abstract class DoubleOrderingAlgorithms : OrderedAlgorithms<double, DoubleOrderingAlgorithms.Order> {
        public struct Order : IOrdering<double> {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(double a, double b) => a < b || !(a >= b);
        }
    }

    public abstract class BigStructOrderingAlgorithms : OrderedAlgorithms<(int, long, DateTime, string), BigStructOrderingAlgorithms.Order> {
        public struct Order : IOrdering<(int, long, DateTime, string)> {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan((int, long, DateTime, string) a, (int, long, DateTime, string) b) => a.Item1 < b.Item1 || a.Item1 == b.Item1 && a.Item2 < b.Item2;
        }
    }

    public abstract class SmallStructOrderingAlgorithms : OrderedAlgorithms<(int, int, int), SmallStructOrderingAlgorithms.Order> {
        public struct Order : IOrdering<(int, int, int)> {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan((int, int, int) a, (int, int, int) b) => a.Item1 < b.Item1 || a.Item1 == b.Item1 && a.Item2 < b.Item2;
        }
    }

    public class SampleClass : IComparable<SampleClass> {
        public int Value;
        public int CompareTo(SampleClass other) => Value.CompareTo(other.Value);
    }

    public abstract class SampleClassOrderingAlgorithms : OrderedAlgorithms<SampleClass, SampleClassOrderingAlgorithms.Order> {
        public struct Order : IOrdering<SampleClass> {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(SampleClass a, SampleClass b) => a.Value < b.Value;
        }
    }
}
