using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using IncrementalMeanVarianceAccumulator;
using ExpressionToCodeLib;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
namespace SortAlgoBench
{
    static class SortAlgoBenchProgram
    {
        public const int MaxArraySize = 1 << 15 << 3;
        public const int TimingTrials = 200;
        public const int IterationsPerTrial = 40;
        public static readonly int ParallelSplitScale = Helpers.ProcScale();

        static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
            Int32OrderingAlgorithms.BencherFor(Helpers.RandomizeInt32()).BenchVariousAlgos();
            UInt64OrderingAlgorithms.BencherFor(Helpers.RandomizeUInt64()).BenchVariousAlgos();
            SampleClassOrderingAlgorithms.BencherFor(Helpers.RandomizeSampleClass()).BenchVariousAlgos();
            BigStructOrderingAlgorithms.BencherFor(Helpers.RandomizeBigStruct()).BenchVariousAlgos();
            ComparableOrderingAlgorithms<int>.BencherFor(Helpers.RandomizeInt32()).BenchVariousAlgos();
            //ComparableOrderingAlgorithms<SampleClass>.BencherFor(Helpers.RandomizeSampleClass()).BenchVariousAlgos();
            UInt32OrderingAlgorithms.BencherFor(Helpers.RandomizeUInt32()).BenchVariousAlgos();
        }
    }

    sealed class SortAlgorithmBench<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
        public void BenchVariousAlgos()
        {
            Console.WriteLine("Benchmarking array of " + typeof(T).ToCSharpFriendlyTypeName() + " with ordering " + typeof(TOrder).ToCSharpFriendlyTypeName() + " (where relevant)");
            //BenchSort(SystemArraySort);
            BenchSort(OrderedAlgorithms<T, TOrder>.QuickSort);
            BenchSort(OrderedAlgorithms<T, TOrder>.ParallelQuickSort);
            //BenchSort(OrderedAlgorithms<T, TOrder>.BottomUpMergeSort);
            //BenchSort(OrderedAlgorithms<T, TOrder>.BottomUpMergeSort2);
            //BenchSort(OrderedAlgorithms<T, TOrder>.TopDownMergeSort);
            //BenchSort(OrderedAlgorithms<T, TOrder>.AltTopDownMergeSort);
            //BenchSort(OrderedAlgorithms<T, TOrder>.ParallelDualPivotQuickSort);
            //BenchSort(OrderedAlgorithms<T, TOrder>.ParallelTopDownMergeSort);

            Console.WriteLine();
        }

        static void SystemArraySort(T[] arr, int len) { Array.Sort(arr, 0, len); }

        public SortAlgorithmBench(T[] sourceData)
        {
            this.sourceData = sourceData;
            workspace = new T[sourceData.Length >> 3];
        }

        readonly T[] workspace;
        readonly T[] sourceData;

        int RefreshData(Random random)
        {
            var len = random.Next(workspace.Length + 1);
            var offset = random.Next(sourceData.Length - len + 1);
            Array.Copy(sourceData, offset, workspace, 0, len);
            return len;
        }

        public void BenchSort(Action<T[], int> action)
        {
            var txt = action.Method.Name + "|" + typeof(T).ToCSharpFriendlyTypeName();
            Validate(action, txt); //also a warmup
            var sizes = new List<int>();
            var milliseconds = new List<double>();
            for (var i = 0; i < SortAlgoBenchProgram.TimingTrials; i++) {
                var random = new Random(42);
                var sw = new Stopwatch();
                for (var k = 0; k < SortAlgoBenchProgram.IterationsPerTrial; k++) {
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
            Console.WriteLine($"{txt}: {Helpers.MSE(msDistrib)} (ms) for {sizes.Count} arrays of on average {meanLen:f1} items");
        }

        public void Validate(Action<T[], int> action, string txt)
        {
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

    abstract class ComparableOrderingAlgorithms<T> : OrderedAlgorithms<T, ComparableOrderingAlgorithms<T>.ComparableOrdering>
        where T : IComparable<T>
    {
        public struct ComparableOrdering : IOrdering<T>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(T a, T b) => a.CompareTo(b) < 0;
        }
    }

    abstract class UInt64OrderingAlgorithms : OrderedAlgorithms<ulong, UInt64OrderingAlgorithms.UInt64Ordering>
    {
        public struct UInt64Ordering : IOrdering<ulong>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(ulong a, ulong b) => a < b;
        }
    }

    abstract class UInt32OrderingAlgorithms : OrderedAlgorithms<uint, UInt32OrderingAlgorithms.UInt32Order>
    {
        public struct UInt32Order : IOrdering<uint>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(uint a, uint b) => a < b;
        }
    }

    abstract class Int32OrderingAlgorithms : OrderedAlgorithms<int, Int32OrderingAlgorithms.Int32Order>
    {
        public struct Int32Order : IOrdering<int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(int a, int b) => a < b;
        }
    }

    abstract class BigStructOrderingAlgorithms : OrderedAlgorithms<(int, long, DateTime, string), BigStructOrderingAlgorithms.Order>
    {
        public struct Order : IOrdering<(int, long, DateTime, string)>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan((int, long, DateTime, string) a, (int, long, DateTime, string) b) => a.Item1 < b.Item1 || a.Item1 == b.Item1 && a.Item2 < b.Item2;
        }
    }

    class SampleClass : IComparable<SampleClass>
    {
        public int Value;
        public int CompareTo(SampleClass other) => Value.CompareTo(other.Value);
    }

    abstract class SampleClassOrderingAlgorithms : OrderedAlgorithms<SampleClass, SampleClassOrderingAlgorithms.Order>
    {
        public struct Order : IOrdering<SampleClass>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(SampleClass a, SampleClass b) => a.Value < b.Value;
        }
    }
}
