using System;

namespace SortAlgoBench
{
    public static class ParallelismConstants
    {
        static int ProcScale()
        {
            var splitIters = 3;
            var threads = Environment.ProcessorCount;
            while (threads > 0) {
                threads = threads >> 1;
                splitIters++;
            }

            return splitIters;
        }

        public static readonly int ParallelSplitScale = ProcScale();
    }
}
