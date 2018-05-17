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
            const int quality = 1;
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
            var large = BenchSize(1 << 14 << 4, 20*quality, 10*quality);
            Console.WriteLine();
            var med=BenchSize(1 << 9 << 4, 50*quality, 80*quality);
            Console.WriteLine();
            var small =BenchSize(1 << 7 << 4, 100*quality, 200*quality);
            var all = new []{small, med,large }.SelectMany(x=>x).ToArray();

            Console.WriteLine();
            foreach(var byType in all.GroupBy(o=>o.type))
                Console.WriteLine($"{byType.Key.ToCSharpFriendlyTypeName()}: {byType.Average(o=>o.nsPerArrayItem):f1}ns/item");

            Console.WriteLine();
            foreach(var byMethod in all.GroupBy(o=>o.method))
                Console.WriteLine($"{byMethod.Key}: {byMethod.Average(o=>o.nsPerArrayItem):f1}ns/item");

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"OVERALL: {all.Average(o=>o.nsPerArrayItem):f1}ns/item");
        }

        private static (string method, Type type, double nsPerArrayItem)[] BenchSize(int MaxArraySize, int TimingTrials, int IterationsPerTrial) {
            var data = Helpers.RandomizeUInt64(MaxArraySize);
            return new []{
                DoubleOrderingAlgorithms.BencherFor(Helpers.MapToDouble(data), TimingTrials * 3 / 2, IterationsPerTrial).BenchVariousAlgos(),
                Int32OrderingAlgorithms.BencherFor(Helpers.MapToInt32(data), TimingTrials * 3 / 2, IterationsPerTrial).BenchVariousAlgos(),
                SmallStructOrderingAlgorithms.BencherFor(Helpers.MapToSmallStruct(data), TimingTrials, IterationsPerTrial).BenchVariousAlgos(),
                SampleClassOrderingAlgorithms.BencherFor(Helpers.MapToSampleClass(data), TimingTrials * 2 / 3, IterationsPerTrial).BenchVariousAlgos(),
                UInt64OrderingAlgorithms.BencherFor(data, TimingTrials, IterationsPerTrial).BenchVariousAlgos(),
                BigStructOrderingAlgorithms.BencherFor(Helpers.MapToBigStruct(data), TimingTrials / 2, IterationsPerTrial).BenchVariousAlgos(),
                UInt32OrderingAlgorithms.BencherFor(Helpers.MapToUInt32(data), TimingTrials * 3 / 2, IterationsPerTrial).BenchVariousAlgos(),
                ComparableOrderingAlgorithms<int>.BencherFor(Helpers.MapToInt32(data), TimingTrials * 3 / 2, IterationsPerTrial).BenchVariousAlgos(),
            }.SelectMany(r=>r).ToArray();
        }
    }

    public sealed class SortAlgorithmBench<T, TOrder>
        where TOrder : struct, IOrdering<T> {
        public IEnumerable<(string method, Type type, double nsPerArrayItem)> BenchVariousAlgos() {
            Console.WriteLine("Benchmarking array of " + typeof(T).ToCSharpFriendlyTypeName() + " with ordering " + typeof(TOrder).ToCSharpFriendlyTypeName() + " (where relevant)");
            yield return BenchSort(SystemArraySort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.DualPivotQuickSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.QuickSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.ParallelQuickSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.BottomUpMergeSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.BottomUpMergeSort2);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.TopDownMergeSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.AltTopDownMergeSort);
            yield return BenchSort(OrderedAlgorithms<T, TOrder>.ParallelTopDownMergeSort);

            Console.WriteLine();
        }

        static void ArraySort_Primitive(T[] arr, int len) { Array.Sort(arr, 0, len); }
        static void ArraySort_OrderComparer(T[] arr, int len) { Array.Sort(arr, 0, len, Helpers.ComparerFor<T, TOrder>()); }

        static readonly Action<T[], int> SystemArraySort = typeof(T).IsPrimitive ? (Action<T[], int>)ArraySort_Primitive : ArraySort_OrderComparer;

        public SortAlgorithmBench(T[] sourceData, int TimingTrials, int IterationsPerTrial) {
            this.sourceData = sourceData;
            this.TimingTrials = TimingTrials;
            this.IterationsPerTrial = IterationsPerTrial;
            workspace = new T[sourceData.Length >> 3];
        }

        readonly T[] workspace;
        readonly T[] sourceData;
        readonly int TimingTrials;
        readonly int IterationsPerTrial;

        int RefreshData(Random random) {
            var len = random.Next(workspace.Length + 1);
            var offset = random.Next(sourceData.Length - len + 1);
            Array.Copy(sourceData, offset, workspace, 0, len);
            return len;
        }

        public (string method, Type type, double nsPerArrayItem) BenchSort(Action<T[], int> action) {
            var txt = action.Method.Name + "|" + typeof(T).ToCSharpFriendlyTypeName();
            Validate(action, txt); //also a warmup
            var sizes = new List<int>();
            var milliseconds = new List<double>();
            for (var i = 0; i < TimingTrials; i++) {
                var random = new Random(42);
                var sw = new Stopwatch();
                for (var k = 0; k < IterationsPerTrial; k++) {
                    var len = RefreshData(random);
                    sw.Start();
                    action(workspace, len);
                    sw.Stop();
                    if (i == 0)
                        sizes.Add(len);
                }

                milliseconds.Add(sw.Elapsed.TotalMilliseconds);
            }

            milliseconds.Sort();

            var msDistrib = MeanVarianceAccumulator.FromSequence(milliseconds.Take(milliseconds.Count >> 1));
            var meanLen = sizes.Average();
            double nsPerArrayItem = msDistrib.Mean / sizes.Sum() * 1000_000;
            Console.WriteLine($"{txt}: {Helpers.MSE(msDistrib)} (ms) over {TimingTrials} runs for {sizes.Count} arrays of on average {meanLen:f1} items: {nsPerArrayItem:f1}ns/item");
            return (action.Method.Name.StartsWith("ArraySort_") ? "ArraySort" : action.Method.Name, typeof(T), nsPerArrayItem);
        }

        public void Validate(Action<T[], int> action, string txt) {
            var random = new Random(42);
            var sw = new Stopwatch();
            for (var k = 0; k < 10; k++) {
                var len = RefreshData(random);
                long checkSum = 0;
                for (var j = 0; j < len; j++) {
                    var l = workspace[j];
                    checkSum = checkSum + l.GetHashCode();
                }

                if (len > 50) {
                    var sorted = true;
                    for (var j = 1; j < len; j++)
                        if (!default(TOrder).LessThan(workspace[j], workspace[j - 1]))
                            sorted = false;
                    if (sorted) {
                        Console.WriteLine("Already sorted??");
                        break;
                    }
                }

                sw.Start();
                action(workspace, len);
                sw.Stop();
                for (var j = 0; j < len; j++) {
                    var l = workspace[j];
                    checkSum = checkSum - l.GetHashCode();
                }

                if (checkSum != 0)
                    Console.WriteLine(txt + " has differing elements before and after sort");
                for (var j = 1; j < len; j++)
                    if (default(TOrder).LessThan(workspace[j], workspace[j - 1])) {
                        Console.WriteLine(txt + " did not sort.");
                        break;
                    }
            }
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
            public bool LessThan(double a, double b) =>  a<b || !(a>=b);
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
