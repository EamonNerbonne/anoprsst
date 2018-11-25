using System;
using System.Runtime.CompilerServices;

namespace Anoprsst
{
    public struct AlgorithmChoiceThresholds<T>
    {
        public int TopDownInsertionSortBatchSize;
        public int BottomUpInsertionSortBatchSize;
        
        public int QuickSortFastMedianThreshold;//must be greater than 1.
        public int MinimalParallelQuickSortBatchSize;

        public static AlgorithmChoiceThresholds<T> Defaults {
            get {
                if (!typeof(T).IsValueType) {
                    return new AlgorithmChoiceThresholds<T> {
                        TopDownInsertionSortBatchSize = 24,
                        BottomUpInsertionSortBatchSize = 16,
                        QuickSortFastMedianThreshold = 10_000,
                        MinimalParallelQuickSortBatchSize = 1500,
                    };
                } else if (Unsafe.SizeOf<T>() <= 8) {
                    return new AlgorithmChoiceThresholds<T> {
                        TopDownInsertionSortBatchSize = 64,
                        BottomUpInsertionSortBatchSize = 40,
                        QuickSortFastMedianThreshold = 13_000,
                        MinimalParallelQuickSortBatchSize = 1100,
                    };
                } else {
                    var topDownInsertionSortBatchSize = Math.Max(8, 550 / Unsafe.SizeOf<T>());
                    return new AlgorithmChoiceThresholds<T> {
                        TopDownInsertionSortBatchSize = topDownInsertionSortBatchSize,
                        BottomUpInsertionSortBatchSize = topDownInsertionSortBatchSize * 2 / 3,
                        QuickSortFastMedianThreshold = 16_000,
                        MinimalParallelQuickSortBatchSize = 1000,
                    };
                }
            }
        }
    }
}
