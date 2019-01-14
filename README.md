# anoprsst
Sorts `Span<T>` and `T[]` more quickly than `Array.Sort`. It's around 45% faster in serial mode, and upto a core-count-based parallelisation speedup for large arrays, though [details matter](https://github.com/EamonNerbonne/anoprsst/blob/253f97e1d6d1e666dfdf6fb613c41573af19cdc1/results-1.1-netcore2.2.txt): it's no faster for strings, just 20% faster for integral builtins, but more than 100% faster for structs with custom orderings.

Usage:  

 - Add a reference to [Anoprsst on nuget](https://www.nuget.org/packages/Anoprsst/)
 - Implement [`IOrdering`](https://github.com/EamonNerbonne/anoprsst/blob/51e9b4e065ac1ae1cdea88a0c95f3b21ceb69284/src/Anoprsst/OrderedAlgorithms.cs#L8)
with a struct, which defines a `LessThan` method to compare to elements.
 - Call `someSpan.WithOrder(new MyOrdering()).Sort()`, which uses an insertion sort for small arrays, quicksort for medium size arrays, and parallel quick sort for large arrays.
 - If you're sorting arrays, the same methods are available there too, but it's worth pointing out the [`someArray.AsSpan(start, length)`](https://docs.microsoft.com/en-gb/dotnet/api/system.memoryextensions.asspan) extension method in System.Memory that makes
   slicing really easy.
 - If the element type is an IComparable *struct*, you can instead use `someSpan.WithIComparableOrder().Sort()`, which is often no slower, and thereby avoid implementing `IOrdering<T>`
 - If you have specific needs concerning the sorting algorithm, numerous alternatives to `.Sort()` are implemented as extension 
   methods in the `Anoprsst.Uncommon` namespace, include a merge sort (stable, without implausible O(N<sup>2</sup>) behavior) and a serial quick sort.
 - Bug reports, pull requests, chatter: all welcome, just give a shout in the issues here on github.
 - Finally: this is thoroughly tried, but still fairly new, and uses unsafe constructs heavily. Nasty bugs are a possibility; use at your own risk.
 
 
In terms of performance: Anorpsst's QuickSort outperforms Array.Sort in all cases I tried.  The difference is small for large arrays of primitive types such as an `int` [(Array.Sort: 64.1 ns/item vs. QuickSort: 56.8 ns/item, ParallelQuickSort 10.3 ns/item)](https://github.com/EamonNerbonne/anoprsst/blob/078a501558a72c1ee6936aec7a98719d437c1f44/results-1.0-netcore2.2.txt#L936), considerably larger for reference types [(203 vs. 140 vs. 34.5 ns/item)](https://github.com/EamonNerbonne/anoprsst/blob/078a501558a72c1ee6936aec7a98719d437c1f44/results-1.0-netcore2.2.txt#L1044) and structs [(147 vs. 88.1 vs. 16.3 ns/item)](https://github.com/EamonNerbonne/anoprsst/blob/078a501558a72c1ee6936aec7a98719d437c1f44/results-1.0-netcore2.2.txt#L1032).  Smaller arrays/spans benefit more, such as for arrays of ints of approximately 128 elements: [(Array.Sort: 17.861 ns/item vs. QuickSort: 13.373 ns/item, ParallelQuickSort 13.786 ns/item)](https://github.com/EamonNerbonne/anoprsst/blob/078a501558a72c1ee6936aec7a98719d437c1f44/results-1.0-netcore2.2.txt#L138).  All measurements done on .net core2.2; Custom IComparable implementations are particularly slow on the full .net framework, making the performance advantages particularly stark on the .net framework.

Feedback; PRs; bug reports; questions or plain cheers & jeers: please post in the [issues on github](https://github.com/EamonNerbonne/anoprsst/issues).
