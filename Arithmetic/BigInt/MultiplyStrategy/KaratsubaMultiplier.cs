using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class KaratsubaMultiplier : IMultiplier
{
    private const int SchoolThreshold = 32;

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
        ReadOnlySpan<uint> xHigh = xLen > m
            ? x.Slice(m, xLen - m)
            : ReadOnlySpan<uint>.Empty;

        ReadOnlySpan<uint> yLow = y.Slice(0, Math.Min(m, yLen));
        ReadOnlySpan<uint> yHigh = yLen > m
            ? y.Slice(m, yLen - m)
            : ReadOnlySpan<uint>.Empty;

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
            for (int j = 0; j < bLen; j++)
            {
                AddUIntProductByHalves(
                    result,
                    i + j,
                    a[i],
                    b[j]);
            }
        }

        return Trim(result);
    }

    private static void AddUIntProductByHalves(
        uint[] result,
        int index,
        uint left,
        uint right)
    {
        /*
         * Разбиваем uint на две половины.
         *
         * left = leftLow + leftHigh * 2^HalfUIntBits
         * right = rightLow + rightHigh * 2^HalfUIntBits
         */
        uint leftLow = left & HalfMask;
        uint leftHigh = left >> HalfUIntBits;

        uint rightLow = right & HalfMask;
        uint rightHigh = right >> HalfUIntBits;

        /*
         * Каждое произведение половинок помещается в uint.
         *
         * Для стандартного uint:
         *
         * HalfUIntBits = 16
         *
         * Значит максимум:
         *
         * 0xFFFF * 0xFFFF = 0xFFFE0001
         *
         * Это меньше uint.MaxValue.
         */
        uint p00 = leftLow * rightLow;
        uint p01 = leftLow * rightHigh;
        uint p10 = leftHigh * rightLow;
        uint p11 = leftHigh * rightHigh;

        /*
         * left * right =
         *
         * p00
         * + (p01 << HalfUIntBits)
         * + (p10 << HalfUIntBits)
         * + (p11 << UIntBits)
         *
         * p11 << UIntBits означает, что p11 надо добавить
         * уже в следующее uint-слово.
         */
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
        /*
         * product * 2^HalfUIntBits может занимать два uint-слова.
         *
         * Младшая часть:
         *
         * product << HalfUIntBits
         *
         * Старшая часть:
         *
         * product >> HalfUIntBits
         */
        uint lowPart = product << HalfUIntBits;
        uint highPart = product >> HalfUIntBits;

        AddUInt(result, index, lowPart);
        AddUInt(result, index + 1, highPart);
    }

    private static uint[] AddMagnitudes(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int aLen = TrimmedLength(a);
        int bLen = TrimmedLength(b);
        int maxLen = Math.Max(aLen, bLen);

        uint[] result = new uint[maxLen + 1];

        uint carry = 0;

        for (int i = 0; i < maxLen; i++)
        {
            uint av = i < aLen ? a[i] : 0u;
            uint bv = i < bLen ? b[i] : 0u;

            result[i] = AddThreeUInt(av, bv, carry, out carry);
        }

        result[maxLen] = carry;

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

        uint borrow = 0;

        for (int i = 0; i < aLen; i++)
        {
            uint av = a[i];
            uint bv = i < bLen ? b[i] : 0u;

            result[i] = SubtractTwoUInt(av, bv, borrow, out borrow);
        }

        return Trim(result);
    }

    private static uint[] Combine(
        ReadOnlySpan<uint> z0,
        ReadOnlySpan<uint> middle,
        ReadOnlySpan<uint> z2,
        int shiftWords)
    {
        int resultLen = Math.Max(
            z0.Length,
            Math.Max(
                middle.Length + shiftWords,
                z2.Length + 2 * shiftWords)) + 1;

        uint[] result = new uint[resultLen];

        AddShifted(result, z0, 0);
        AddShifted(result, middle, shiftWords);
        AddShifted(result, z2, 2 * shiftWords);

        return Trim(result);
    }

    private static void AddShifted(
        uint[] target,
        ReadOnlySpan<uint> source,
        int shiftWords)
    {
        if (source.Length == 0)
            return;

        for (int i = 0; i < source.Length; i++)
        {
            AddUInt(target, i + shiftWords, source[i]);
        }
    }

    private static void AddUInt(uint[] result, int index, uint value)
    {
        while (value != 0)
        {
            if (index >= result.Length)
                throw new InvalidOperationException("Result buffer overflow.");

            uint old = result[index];
            uint sum = old + value;

            result[index] = sum;

            /*
             * Если uint переполнился, результат стал меньше old.
             *
             * Например:
             *
             * old   = 0xFFFFFFFF
             * value = 1
             * sum   = 0x00000000
             *
             * Значит надо перенести 1 в следующее слово.
             */
            value = sum < old ? 1u : 0u;

            index++;
        }
    }

    private static uint AddThreeUInt(
        uint a,
        uint b,
        uint carryIn,
        out uint carryOut)
    {
        /*
         * Складываем a + b + carryIn без ulong.
         *
         * Сначала складываем a + b.
         * Если произошло переполнение, sum1 < a.
         */
        uint sum1 = a + b;
        uint carry1 = sum1 < a ? 1u : 0u;

        /*
         * Потом добавляем carryIn.
         * carryIn всегда равен 0 или 1.
         */
        uint sum2 = sum1 + carryIn;
        uint carry2 = sum2 < sum1 ? 1u : 0u;

        /*
         * При сложении двух uint и одного carry итоговый перенос
         * может быть только 0 или 1.
         */
        carryOut = carry1 | carry2;

        return sum2;
    }

    private static uint SubtractTwoUInt(
        uint a,
        uint b,
        uint borrowIn,
        out uint borrowOut)
    {
        /*
         * Считаем:
         *
         * a - b - borrowIn
         *
         * borrowIn всегда 0 или 1.
         *
         * Сначала формируем полный вычитаемый элемент:
         *
         * subtrahend = b + borrowIn
         *
         * Но это сложение само может переполнить uint,
         * если b == uint.MaxValue и borrowIn == 1.
         */
        uint subtrahend = b + borrowIn;
        uint borrowFromSubtrahend = subtrahend < b ? 1u : 0u;

        /*
         * Если b + borrowIn переполнилось,
         * это означает, что реально мы вычитаем 2^UIntBits.
         *
         * Для текущего слова результатом будет a - 0 == a,
         * но в следующее слово обязательно уйдёт borrow.
         */
        if (borrowFromSubtrahend != 0)
        {
            borrowOut = 1u;
            return a;
        }

        /*
         * Теперь обычное вычитание a - subtrahend.
         *
         * Если a < subtrahend, то нужен заём из следующего слова.
         */
        if (a < subtrahend)
        {
            borrowOut = 1u;
            return a - subtrahend;
        }

        borrowOut = 0u;
        return a - subtrahend;
    }

    private static int CompareMagnitudes(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int aLen = TrimmedLength(a);
        int bLen = TrimmedLength(b);

        if (aLen > bLen)
            return 1;

        if (aLen < bLen)
            return -1;

        for (int i = aLen - 1; i >= 0; i--)
        {
            if (a[i] > b[i])
                return 1;

            if (a[i] < b[i])
                return -1;
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