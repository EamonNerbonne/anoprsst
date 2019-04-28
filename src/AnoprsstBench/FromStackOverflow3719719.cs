namespace AnoprsstBench
{
    public static class FromStackOverflow3719719
    {
        //Fixed versions of https://stackoverflow.com/questions/3719719/fastest-safe-sorting-algorithm-implementation

        public static void QuickSort(int[] data, int left, int right)
        {
            if (right - left < 2)
                return;
            QuickSortRecursive(data, left, right);
        }

        static void QuickSortRecursive(int[] data, int left, int right)
        {
            var d = data[left];

            var i = left - 1;
            var j = right;

            while (true) {
                do {
                    i++;
                } while (data[i] < d);

                do {
                    j--;
                } while (data[j] > d);

                if (i < j) {
                    var tmp = data[i];
                    data[i] = data[j];
                    data[j] = tmp;
                } else {
                    if (left < j)
                        QuickSortRecursive(data, left, j + 1);
                    if (j + 2 < right)
                        QuickSortRecursive(data, j + 1, right);
                    return;
                }
            }
        }

        public static unsafe void UnsafeQuickSort(int[] data, int left, int right)
        {
            if (right - left < 2)
                return;
            fixed (int* pdata = data) {
                UnsafeQuickSortRecursive(pdata, left, right);
            }
        }

        static unsafe void UnsafeQuickSortRecursive(int* data, int left, int right)
        {
            var d = data[left];

            var i = left - 1;
            var j = right;

            while (true) {
                do {
                    i++;
                } while (data[i] < d);

                do {
                    j--;
                } while (data[j] > d);

                if (i < j) {
                    var tmp = data[i];
                    data[i] = data[j];
                    data[j] = tmp;
                } else {
                    if (left < j)
                        UnsafeQuickSortRecursive(data, left, j + 1);
                    if (j + 2 < right)
                        UnsafeQuickSortRecursive(data, j + 1, right);
                    return;
                }
            }
        }
    }
}
