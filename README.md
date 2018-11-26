# anoprsst
Sorts Span&lt;T> and arrays more quickly than Array.Sort

Usage:

 - Implement [`IOrdering`](https://github.com/EamonNerbonne/anoprsst/blob/4c77b32169606074840a37024bd7fbd46ea62055/src/Anorpsst/OrderedAlgorithms.cs#L8)
with a struct, which defines a `LessThan` method to compare to elements.
 - Call `someSpan.SortUsing(new MyOrdering()).Sort()`, which uses a insertion sort for small arrays, quicksort for medium size arrays, and parallel quick sort for large arrays.
 - If you're sorting arrays, the same methods are available there too, but it's worth pointing out the `someArray.AsSpan(start, length)` extension method that makes
   slicing really easy/
 - If the element type is an IComparable *struct*, you can instead use `someSpan.SortIComparableUsing().Sort()`, which is often no slower, and thereby avoid implementing `IOrdering<T>`
 - If you have specific needs concerning the sorting algorithm, numerous alternatives to `.Sort()` are implemented as extension 
   methods in the `Anoprsst.Uncommon` namespace, include a merge sort (stable, without implausible O(N<sup>2</sup>) behavior) and a serial quick sort.
 - Bug reports, pull requests, chatter: all welcome, just give a shout in the issues here on github.
 - Finally: this is thoroughly tried, but still fairly new, and uses unsafe constructs heavily. Nasty bugs are a possibility; use at your own risk.
 
 
