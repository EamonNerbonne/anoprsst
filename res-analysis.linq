<Query Kind="Statements">
  <Namespace>System.Linq</Namespace>
</Query>

var records = @"int/Int32Ordering using PrimitiveArraySort: 39.6ns/item
int/Int32Ordering using ParallelQuickSort: 13.9ns/item
int/Int32Ordering using QuickSort: 33.3ns/item
int/Int32Ordering using TopDownMergeSort: 36.8ns/item
int/Int32Ordering using BottomUpMergeSort: 37.5ns/item
int/Int32Ordering using DualPivotQuickSort: 34.3ns/item
int/Int32Ordering using AltTopDownMergeSort: 37.2ns/item
int/ComparableOrdering<int> using IComparableArraySort: 64.1ns/item
int/ComparableOrdering<int> using ParallelQuickSort: 14.7ns/item
int/ComparableOrdering<int> using QuickSort: 34.9ns/item
int/ComparableOrdering<int> using TopDownMergeSort: 38.6ns/item
int/ComparableOrdering<int> using BottomUpMergeSort: 38.7ns/item
int/ComparableOrdering<int> using DualPivotQuickSort: 37.3ns/item
int/ComparableOrdering<int> using AltTopDownMergeSort: 38.4ns/item
uint/UInt32Ordering using PrimitiveArraySort: 40.9ns/item
uint/UInt32Ordering using ParallelQuickSort: 14.2ns/item
uint/UInt32Ordering using QuickSort: 33.9ns/item
uint/UInt32Ordering using TopDownMergeSort: 37.5ns/item
uint/UInt32Ordering using BottomUpMergeSort: 37.7ns/item
uint/UInt32Ordering using DualPivotQuickSort: 35.0ns/item
uint/UInt32Ordering using AltTopDownMergeSort: 37.5ns/item
uint/ComparableOrdering<uint> using IComparableArraySort: 61.8ns/item
uint/ComparableOrdering<uint> using ParallelQuickSort: 14.3ns/item
uint/ComparableOrdering<uint> using QuickSort: 34.0ns/item
uint/ComparableOrdering<uint> using TopDownMergeSort: 37.4ns/item
uint/ComparableOrdering<uint> using BottomUpMergeSort: 37.9ns/item
uint/ComparableOrdering<uint> using DualPivotQuickSort: 37.5ns/item
uint/ComparableOrdering<uint> using AltTopDownMergeSort: 37.5ns/item
ulong/UInt64Ordering using PrimitiveArraySort: 41.2ns/item
ulong/UInt64Ordering using ParallelQuickSort: 14.1ns/item
ulong/UInt64Ordering using QuickSort: 33.8ns/item
ulong/UInt64Ordering using TopDownMergeSort: 38.0ns/item
ulong/UInt64Ordering using BottomUpMergeSort: 38.7ns/item
ulong/UInt64Ordering using DualPivotQuickSort: 34.3ns/item
ulong/UInt64Ordering using AltTopDownMergeSort: 37.9ns/item
ulong/ComparableOrdering<ulong> using IComparableArraySort: 60.3ns/item
ulong/ComparableOrdering<ulong> using ParallelQuickSort: 14.4ns/item
ulong/ComparableOrdering<ulong> using QuickSort: 34.1ns/item
ulong/ComparableOrdering<ulong> using TopDownMergeSort: 37.3ns/item
ulong/ComparableOrdering<ulong> using BottomUpMergeSort: 38.5ns/item
ulong/ComparableOrdering<ulong> using DualPivotQuickSort: 36.3ns/item
ulong/ComparableOrdering<ulong> using AltTopDownMergeSort: 37.5ns/item
double/DoubleOrdering using PrimitiveArraySort: 47.7ns/item
double/DoubleOrdering using ParallelQuickSort: 15.8ns/item
double/DoubleOrdering using QuickSort: 37.9ns/item
double/DoubleOrdering using TopDownMergeSort: 42.0ns/item
double/DoubleOrdering using BottomUpMergeSort: 43.6ns/item
double/DoubleOrdering using DualPivotQuickSort: 38.5ns/item
double/DoubleOrdering using AltTopDownMergeSort: 42.3ns/item
double/ComparableOrdering<double> using IComparableArraySort: 101.1ns/item
double/ComparableOrdering<double> using ParallelQuickSort: 27.7ns/item
double/ComparableOrdering<double> using QuickSort: 65.9ns/item
double/ComparableOrdering<double> using TopDownMergeSort: 71.0ns/item
double/ComparableOrdering<double> using BottomUpMergeSort: 69.3ns/item
double/ComparableOrdering<double> using DualPivotQuickSort: 67.3ns/item
double/ComparableOrdering<double> using AltTopDownMergeSort: 69.3ns/item
float/FloatOrdering using PrimitiveArraySort: 47.1ns/item
float/FloatOrdering using ParallelQuickSort: 15.7ns/item
float/FloatOrdering using QuickSort: 38.0ns/item
float/FloatOrdering using TopDownMergeSort: 41.6ns/item
float/FloatOrdering using BottomUpMergeSort: 42.5ns/item
float/FloatOrdering using DualPivotQuickSort: 38.8ns/item
float/FloatOrdering using AltTopDownMergeSort: 41.9ns/item
float/ComparableOrdering<float> using IComparableArraySort: 103.2ns/item
float/ComparableOrdering<float> using ParallelQuickSort: 27.3ns/item
float/ComparableOrdering<float> using QuickSort: 64.7ns/item
float/ComparableOrdering<float> using TopDownMergeSort: 69.4ns/item
float/ComparableOrdering<float> using BottomUpMergeSort: 67.3ns/item
float/ComparableOrdering<float> using DualPivotQuickSort: 66.0ns/item
float/ComparableOrdering<float> using AltTopDownMergeSort: 68.6ns/item
(int, int, int)/SmallTupleOrder using IComparableArraySort: 93.1ns/item
(int, int, int)/SmallTupleOrder using ParallelQuickSort: 26.8ns/item
(int, int, int)/SmallTupleOrder using QuickSort: 54.6ns/item
(int, int, int)/SmallTupleOrder using TopDownMergeSort: 57.0ns/item
(int, int, int)/SmallTupleOrder using BottomUpMergeSort: 89.2ns/item
(int, int, int)/SmallTupleOrder using DualPivotQuickSort: 57.8ns/item
(int, int, int)/SmallTupleOrder using AltTopDownMergeSort: 57.4ns/item
SampleClass/SampleClassOrder using IComparableArraySort: 96.3ns/item
SampleClass/SampleClassOrder using ParallelQuickSort: 27.5ns/item
SampleClass/SampleClassOrder using QuickSort: 65.4ns/item
SampleClass/SampleClassOrder using TopDownMergeSort: 75.9ns/item
SampleClass/SampleClassOrder using BottomUpMergeSort: 83.5ns/item
SampleClass/SampleClassOrder using DualPivotQuickSort: 69.3ns/item
SampleClass/SampleClassOrder using AltTopDownMergeSort: 75.0ns/item
(int, long, DateTime, string, Guid)/BigTupleOrder using IComparableArraySort: 322.3ns/item
(int, long, DateTime, string, Guid)/BigTupleOrder using ParallelQuickSort: 61.3ns/item
(int, long, DateTime, string, Guid)/BigTupleOrder using QuickSort: 146.6ns/item
(int, long, DateTime, string, Guid)/BigTupleOrder using TopDownMergeSort: 188.2ns/item
(int, long, DateTime, string, Guid)/BigTupleOrder using BottomUpMergeSort: 192.7ns/item
(int, long, DateTime, string, Guid)/BigTupleOrder using DualPivotQuickSort: 160.4ns/item
(int, long, DateTime, string, Guid)/BigTupleOrder using AltTopDownMergeSort: 184.8ns/item
string/StringOrder using StringArraySort: 175.4ns/item
string/StringOrder using ParallelQuickSort: 61.7ns/item
string/StringOrder using QuickSort: 171.5ns/item
string/StringOrder using TopDownMergeSort: 172.7ns/item
string/StringOrder using BottomUpMergeSort: 191.7ns/item
string/StringOrder using DualPivotQuickSort: 174.1ns/item
string/StringOrder using AltTopDownMergeSort: 170.0ns/item
".Split(new[] { '\r', '\n'}, StringSplitOptions.RemoveEmptyEntries)
.Select(l=>l.Split(new []{" using ", "/", ": ", "ns/item" },StringSplitOptions.RemoveEmptyEntries))
.Select(l=> new{ type = l[0], order= l[1], sort=l[2], perf=double.Parse( l[3])})
;

records
    .Where(o => !o.order.StartsWith("Comparable", StringComparison.Ordinal))
    .GroupBy(o => o.sort.EndsWith("ArraySort") ? "ArraySort" : o.sort).Select(g => new { sort = g.Key, perf = g.Average(o => o.perf) })
    .Dump();//45% faster

records
    .Where(o => !o.order.StartsWith("Comparable", StringComparison.Ordinal))
    .Where(o => o.type[0] != '(' && o.type != "SampleClass" && o.type != "string" && (o.type == "float" || o.type=="double"))
    .GroupBy(o => o.sort.EndsWith("ArraySort") ? "ArraySort" : o.sort).Select(g => new { sort = g.Key, perf = g.Average(o => o.perf) })
    .Dump();//40% for floating point builtins
records
    .Where(o => !o.order.StartsWith("Comparable", StringComparison.Ordinal))
    .Where(o => o.type[0] != '(' && o.type != "SampleClass" && o.type != "string"  && (o.type != "float" && o.type!="double") )
    .GroupBy(o => o.sort.EndsWith("ArraySort") ? "ArraySort" : o.sort).Select(g => new { sort = g.Key, perf = g.Average(o => o.perf) })
    .Dump();//20% for integral builtins

records
    .Where(o => o.order.StartsWith("Comparable", StringComparison.Ordinal))
    .GroupBy(o => o.sort.EndsWith("ArraySort") ? "ArraySort" : o.sort).Select(g => new { sort = g.Key, perf = g.Average(o => o.perf) })
    .Dump();//70% faster for builtins via IComparable<>



records
    .Where(o => !o.order.StartsWith("Comparable", StringComparison.Ordinal))
    .Where(o => o.type[0] == '(')
    .GroupBy(o => o.sort.EndsWith("ArraySort") ? "ArraySort" : o.sort).Select(g => new { sort = g.Key, perf = g.Average(o => o.perf) })
    .Dump(); //100% for custom structs

records
    .Where(o => !o.order.StartsWith("Comparable", StringComparison.Ordinal))
    .Where(o => o.type == "SampleClass")
    .GroupBy(o => o.sort.EndsWith("ArraySort") ? "ArraySort" : o.sort).Select(g => new { sort = g.Key, perf = g.Average(o => o.perf) })
    .Dump(); //50%


records
    .Where(o => !o.order.StartsWith("Comparable", StringComparison.Ordinal))
    .Where(o => o.type == "string")
    .GroupBy(o => o.sort.EndsWith("ArraySort") ? "ArraySort" : o.sort).Select(g => new { sort = g.Key, perf = g.Average(o => o.perf) })
    .Dump(); //roughly the same.
