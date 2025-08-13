// See https://aka.ms/new-console-template for more information
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

var summary = BenchmarkRunner.Run<Average>();

[SimpleJob]
public class Average
{
    private int[] data = null!;

    [Params(5, 10, 100, 1000, 10000)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        data = new int[N];
        var random = new Random(42);
        for (int i = 0; i < N; i++)
        {
            data[i] = random.Next();
        }
    }

    [Benchmark]
    public double DotNetRuntimeAverage() => data.Average();

    [Benchmark]
    public double Dotnet10Average()
    {
        var span = data.AsSpan();
        if (true)
        {
            // Int32 is special-cased separately from the rest of the types as it can be vectorized:
            // with at most Int32.MaxValue values, and with each being at most Int32.MaxValue, we can't
            // overflow a long accumulator, and order of operations doesn't matter.

            if (span.IsEmpty)
            {
                throw new Exception("Cannot compute average of an empty collection.");
            }

            long sum = 0;
            int i = 0;

            if (Vector.IsHardwareAccelerated && span.Length >= Vector<int>.Count)
            {
                Vector<long> sums = default;
                do
                {
                    Vector.Widen(new Vector<int>(span.Slice(i)), out Vector<long> low, out Vector<long> high);
                    sums += low;
                    sums += high;
                    i += Vector<int>.Count;
                }
                while (i <= span.Length - Vector<int>.Count);
                sum += Vector.Sum(sums);
            }

            for (; (uint)i < (uint)span.Length; i++)
            {
                sum += span[i];
            }

            return (double)sum / span.Length;
        }
    }

    [Benchmark]
    public double MyAverage()
    {
        var span = data.AsSpan();

        // Int32 is special-cased separately from the rest of the types as it can be vectorized:
        // with at most Int32.MaxValue values, and with each being at most Int32.MaxValue, we can't
        // overflow a long accumulator, and order of operations doesn't matter.

        if (span.IsEmpty)
        {
            throw new Exception("Cannot compute average of an empty collection.");
        }

        long sum = 0;
        int i = 0;

        // Try Vector512 first (AVX-512) for best performance on capable hardware
        if (Vector512.IsHardwareAccelerated && span.Length >= Vector512<int>.Count)
        {
            Vector512<long> sums = Vector512<long>.Zero;
            do
            {
                Vector512<int> vector = Vector512.Create<int>(span.Slice(i, Vector512<int>.Count));
                (var low, var high) = Vector512.Widen(vector);
                sums += low;
                sums += high;
                i += Vector512<int>.Count;
            }
            while (i <= span.Length - Vector512<int>.Count);
            sum += Vector512.Sum(sums);
        }
        // Fallback to Vector256 (AVX2) if available
        else if (Vector256.IsHardwareAccelerated && span.Length >= Vector256<int>.Count)
        {
            Vector256<long> sums = Vector256<long>.Zero;
            do
            {
                Vector256<int> vector = Vector256.Create<int>(span.Slice(i, Vector256<int>.Count));
                (var low, var high) = Vector256.Widen(vector);
                sums += low;
                sums += high;
                i += Vector256<int>.Count;
            }
            while (i <= span.Length - Vector256<int>.Count);
            sum += Vector256.Sum(sums);
        }
        // Fallback to Vector128 (SSE) if available
        else if (Vector128.IsHardwareAccelerated && span.Length >= Vector128<int>.Count)
        {
            Vector128<long> sums = Vector128<long>.Zero;
            do
            {
                Vector128<int> vector = Vector128.Create<int>(span.Slice(i, Vector128<int>.Count));
                (var low, var high) = Vector128.Widen(vector);
                sums += low;
                sums += high;
                i += Vector128<int>.Count;
            }
            while (i <= span.Length - Vector128<int>.Count);
            sum += Vector128.Sum(sums);
        }
        // Fallback to generic Vector<T> (adapts to hardware capabilities)
        else if (Vector.IsHardwareAccelerated && span.Length >= Vector<int>.Count)
        {
            Vector<long> sums = Vector<long>.Zero;
            do
            {
                Vector.Widen(new Vector<int>(span.Slice(i)), out Vector<long> low, out Vector<long> high);
                sums += low;
                sums += high;
                i += Vector<int>.Count;
            }
            while (i <= span.Length - Vector<int>.Count);
            sum += Vector.Sum(sums);
        }

        // Process remaining elements with scalar loop
        for (; (uint)i < (uint)span.Length; i++)
        {
            sum += span[i];
        }

        return (double)sum / span.Length;
    }
}