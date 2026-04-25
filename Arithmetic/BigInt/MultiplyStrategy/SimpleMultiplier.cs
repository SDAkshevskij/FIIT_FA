using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class SimpleMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));
        if (b is null)
            throw new ArgumentNullException(nameof(b));

        ReadOnlySpan<uint> aDigits = a.GetDigits();
        ReadOnlySpan<uint> bDigits = b.GetDigits();

        int aLen = TrimmedLength(aDigits);
        int bLen = TrimmedLength(bDigits);

        if (aLen == 0 || bLen == 0)
            return new BetterBigInteger(Array.Empty<uint>(), false);

        uint[] result = new uint[aLen + bLen];

        for (int i = 0; i < aLen; i++)
        {
            ulong carry = 0;

            for (int j = 0; j < bLen; j++)
            {
                ulong cur = (ulong)aDigits[i] * bDigits[j]
                            + result[i + j]
                            + carry;

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

        bool isNegative = (a.IsNegative ^ b.IsNegative) && !IsZeroDigits(result);
        return new BetterBigInteger(result, isNegative);
    }

    private static int TrimmedLength(ReadOnlySpan<uint> digits)
    {
        int len = digits.Length;
        while (len > 0 && digits[len - 1] == 0)
        {
            len--;
        }

        return len;
    }

    private static bool IsZeroDigits(ReadOnlySpan<uint> digits)
    {
        return TrimmedLength(digits) == 0;
    }
}