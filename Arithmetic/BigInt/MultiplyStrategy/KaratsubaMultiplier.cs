using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class KaratsubaMultiplier : IMultiplier
{
    private const int SchoolThreshold = 32;

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));
        if (b is null)
            throw new ArgumentNullException(nameof(b));

        ReadOnlySpan<uint> aDigits = a.GetDigits();
        ReadOnlySpan<uint> bDigits = b.GetDigits();

        uint[] magnitude = MultiplyKaratsuba(aDigits, bDigits);
        bool isNegative = (a.IsNegative ^ b.IsNegative) && !IsZeroDigits(magnitude);

        return new BetterBigInteger(magnitude, isNegative);
    }

    private static uint[] MultiplyKaratsuba(ReadOnlySpan<uint> x, ReadOnlySpan<uint> y)
    {
        int xLen = TrimmedLength(x);
        int yLen = TrimmedLength(y);

        if (xLen == 0 || yLen == 0)
            return Array.Empty<uint>();

        x = x.Slice(0, xLen);
        y = y.Slice(0, yLen);

        if (Math.Min(xLen, yLen) < SchoolThreshold)
            return MultiplySchool(x, y);

        int n = Math.Max(xLen, yLen);
        int m = n / 2;

        ReadOnlySpan<uint> xLow = x.Slice(0, Math.Min(m, xLen));
        ReadOnlySpan<uint> xHigh = xLen > m ? x.Slice(m, xLen - m) : ReadOnlySpan<uint>.Empty;

        ReadOnlySpan<uint> yLow = y.Slice(0, Math.Min(m, yLen));
        ReadOnlySpan<uint> yHigh = yLen > m ? y.Slice(m, yLen - m) : ReadOnlySpan<uint>.Empty;

        uint[] z0 = MultiplyKaratsuba(xLow, yLow);
        uint[] z2 = MultiplyKaratsuba(xHigh, yHigh);

        uint[] xSum = AddMagnitudes(xLow, xHigh);
        uint[] ySum = AddMagnitudes(yLow, yHigh);

        uint[] z1 = MultiplyKaratsuba(xSum, ySum);

        uint[] middle = SubtractMagnitudes(z1, z0);
        middle = SubtractMagnitudes(middle, z2);

        return Combine(z0, middle, z2, m);
    }

    private static uint[] MultiplySchool(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int aLen = TrimmedLength(a);
        int bLen = TrimmedLength(b);

        if (aLen == 0 || bLen == 0)
            return Array.Empty<uint>();

        uint[] result = new uint[aLen + bLen];

        for (int i = 0; i < aLen; i++)
        {
            ulong carry = 0;

            for (int j = 0; j < bLen; j++)
            {
                ulong cur = (ulong)a[i] * b[j] + result[i + j] + carry;
                result[i + j] = (uint)cur;
                carry = cur >> 32;
            }

            int k = i + bLen;
            while (carry != 0)
            {
                ulong cur = (ulong)result[k] + carry;
                result[k] = (uint)cur;
                carry = cur >> 32;
                k++;
            }
        }

        return Trim(result);
    }

    private static uint[] AddMagnitudes(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int aLen = TrimmedLength(a);
        int bLen = TrimmedLength(b);
        int maxLen = Math.Max(aLen, bLen);

        uint[] result = new uint[maxLen + 1];
        ulong carry = 0;

        for (int i = 0; i < maxLen; i++)
        {
            ulong av = i < aLen ? a[i] : 0;
            ulong bv = i < bLen ? b[i] : 0;

            ulong sum = av + bv + carry;
            result[i] = (uint)sum;
            carry = sum >> 32;
        }

        result[maxLen] = (uint)carry;
        return Trim(result);
    }

    private static uint[] SubtractMagnitudes(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int cmp = CompareMagnitudes(a, b);
        if (cmp < 0)
            throw new ArgumentException("SubtractMagnitudes expects a >= b.");

        int aLen = TrimmedLength(a);
        int bLen = TrimmedLength(b);

        uint[] result = new uint[aLen];
        long borrow = 0;

        for (int i = 0; i < aLen; i++)
        {
            long av = a[i];
            long bv = i < bLen ? b[i] : 0;

            long diff = av - bv - borrow;
            if (diff < 0)
            {
                diff += 1L << 32;
                borrow = 1;
            }
            else
            {
                borrow = 0;
            }

            result[i] = (uint)diff;
        }

        return Trim(result);
    }

    private static uint[] Combine(ReadOnlySpan<uint> z0, ReadOnlySpan<uint> middle, ReadOnlySpan<uint> z2, int shiftWords)
    {
        int resultLen = Math.Max(
            z0.Length,
            Math.Max(middle.Length + shiftWords, z2.Length + 2 * shiftWords)) + 1;

        uint[] result = new uint[resultLen];

        AddShifted(result, z0, 0);
        AddShifted(result, middle, shiftWords);
        AddShifted(result, z2, 2 * shiftWords);

        return Trim(result);
    }

    private static void AddShifted(uint[] target, ReadOnlySpan<uint> source, int shiftWords)
    {
        if (source.Length == 0)
            return;

        ulong carry = 0;
        int i = 0;

        for (; i < source.Length; i++)
        {
            int idx = i + shiftWords;
            ulong sum = (ulong)target[idx] + source[i] + carry;
            target[idx] = (uint)sum;
            carry = sum >> 32;
        }

        int k = i + shiftWords;
        while (carry != 0)
        {
            ulong sum = (ulong)target[k] + carry;
            target[k] = (uint)sum;
            carry = sum >> 32;
            k++;
        }
    }

    private static int CompareMagnitudes(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int aLen = TrimmedLength(a);
        int bLen = TrimmedLength(b);

        if (aLen > bLen) return 1;
        if (aLen < bLen) return -1;

        for (int i = aLen - 1; i >= 0; i--)
        {
            if (a[i] > b[i]) return 1;
            if (a[i] < b[i]) return -1;
        }

        return 0;
    }

    private static int TrimmedLength(ReadOnlySpan<uint> digits)
    {
        int len = digits.Length;
        while (len > 0 && digits[len - 1] == 0)
            len--;

        return len;
    }

    private static uint[] Trim(ReadOnlySpan<uint> digits)
    {
        int len = TrimmedLength(digits);
        if (len == 0)
            return Array.Empty<uint>();

        uint[] result = new uint[len];
        for (int i = 0; i < len; i++)
            result[i] = digits[i];

        return result;
    }

    private static bool IsZeroDigits(ReadOnlySpan<uint> digits)
    {
        return TrimmedLength(digits) == 0;
    }
}