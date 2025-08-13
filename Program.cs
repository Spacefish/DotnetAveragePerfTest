// See https://aka.ms/new-console-template for more information
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

var summary = BenchmarkRunner.Run<Average>();

[SimpleJob]
public class Average
{
    private int[] data;

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

        if (span.IsEmpty)
        {
            throw new Exception("Cannot compute average of an empty collection.");
        }

        long sum = 0;
        int i = 0;

        if (Avx512F.IsSupported && span.Length >= Vector512<int>.Count)
        {
            var sums = Vector512<long>.Zero;
            int count = Vector512<int>.Count;
            do
            {
                var vector = Vector512.Create<int>(span.Slice(i, count));
                var v_low_256 = vector.GetLower();
                var v_high_256 = vector.GetUpper();
                sums += Avx512F.ConvertToVector512Int64(v_low_256);
                sums += Avx512F.ConvertToVector512Int64(v_high_256);
                i += count;
            }
            while (i <= span.Length - count);
            sum += Vector512.Sum(sums);
        }
        else if (Vector256.IsSupported && span.Length >= Vector256<int>.Count)
        {
            var sums1 = Vector256<long>.Zero;
            var sums2 = Vector256<long>.Zero;
            int count = Vector256<int>.Count;
            do
            {
                var vector = Vector256.Create<int>(span.Slice(i, count));
                Vector.Widen(vector, out var v1, out var v2);
                sums1 += v1;
                sums2 += v2;
                i += count;
            }
            while (i <= span.Length - count);
            sum += Vector256.Sum(sums1) + Vector256.Sum(sums2);
        }
        else if (Vector128.IsSupported && span.Length >= Vector128<int>.Count)
        {
            var sums = Vector128<long>.Zero;
            int count = Vector128<int>.Count;
            do
            {
                var vector = Vector128.Create<int>(span.Slice(i, count));
                Vector.Widen(vector, out var v1, out var v2);
                sums += v1;
                sums += v2;
                i += count;
            }
            while (i <= span.Length - count);
            sum += Vector128.Sum(sums);
        }

        for (; (uint)i < (uint)span.Length; i++)
        {
            sum += span[i];
        }

        return (double)sum / span.Length;
    }
}