using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class SimpleMultiplier : IMultiplier
{
    private const int BitsPerByte = 8;
    private const int UIntBits = sizeof(uint) * BitsPerByte;
    private const int HalfUIntBits = UIntBits / 2;
    private const uint HalfMask = (1u << HalfUIntBits) - 1u;

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
            for (int j = 0; j < bLen; j++)
            {
                AddUIntProductByHalves(
                    result,
                    i + j,
                    aDigits[i],
                    bDigits[j]);
            }
        }

        bool isNegative = (a.IsNegative ^ b.IsNegative) && !IsZeroDigits(result);
        return new BetterBigInteger(result, isNegative);
    }

    private static void AddUIntProductByHalves(
        uint[] result,
        int index,
        uint left,
        uint right)
    {
        uint leftLow = left & HalfMask;
        uint leftHigh = left >> HalfUIntBits;
        uint rightLow = right & HalfMask;
        uint rightHigh = right >> HalfUIntBits;

        uint p00 = leftLow * rightLow;
        uint p01 = leftLow * rightHigh;
        uint p10 = leftHigh * rightLow;
        uint p11 = leftHigh * rightHigh;
        AddUInt(result, index, p00);

        AddShiftedHalfProduct(result, index, p01);
        AddShiftedHalfProduct(result, index, p10);

        AddUInt(result, index + 1, p11);
    }

    private static void AddShiftedHalfProduct(
        uint[] result,
        int index,
        uint product)
    {
        uint lowPart = product << HalfUIntBits;
        uint highPart = product >> HalfUIntBits;

        AddUInt(result, index, lowPart);
        AddUInt(result, index + 1, highPart);
    }

    private static void AddUInt(uint[] result, int index, uint value)
    {
        while (value != 0)
        {
            if (index >= result.Length)
                throw new InvalidOperationException("Multiplication result buffer overflow.");

            uint old = result[index];
            uint sum = old + value;

            result[index] = sum;
            value = sum < old ? 1u : 0u;

            index++;
        }
    }

    private static int TrimmedLength(ReadOnlySpan<uint> digits)
    {
        int len = digits.Length;

        while (len > 0 && digits[len - 1] == 0)
            len--;

        return len;
    }

    private static bool IsZeroDigits(ReadOnlySpan<uint> digits)
    {
        return TrimmedLength(digits) == 0;
    }
}