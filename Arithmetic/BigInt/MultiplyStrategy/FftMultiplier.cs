using System;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    private const int MinTransformLength = 16;
    private const int GuardBits = 4;

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));

        if (b is null)
            throw new ArgumentNullException(nameof(b));

        ReadOnlySpan<uint> aWords = a.GetDigits();
        ReadOnlySpan<uint> bWords = b.GetDigits();

        int aLen = TrimmedLength(aWords);
        int bLen = TrimmedLength(bWords);

        if (aLen == 0 || bLen == 0)
            return new BetterBigInteger(Array.Empty<uint>(), false);

        aWords = aWords.Slice(0, aLen);
        bWords = bWords.Slice(0, bLen);

        uint[] magnitude = MultiplyBySchonhageStrassen(aWords, bWords);

        bool isNegative = (a.IsNegative ^ b.IsNegative) && !IsZeroDigits(magnitude);

        return new BetterBigInteger(magnitude, isNegative);
    }

    private static uint[] MultiplyBySchonhageStrassen(
        ReadOnlySpan<uint> aWords,
        ReadOnlySpan<uint> bWords)
    {
        int aBits = BitLength(aWords);
        int bBits = BitLength(bWords);

        if (aBits == 0 || bBits == 0)
            return Array.Empty<uint>();

        SsParameters p = ChooseParameters(aBits, bBits);

        RingContext ctx = new RingContext(p.RingBits);

        RingElement[] fa = new RingElement[p.TransformLength];
        RingElement[] fb = new RingElement[p.TransformLength];

        for (int i = 0; i < p.TransformLength; i++)
        {
            fa[i] = ctx.Zero;
            fb[i] = ctx.Zero;
        }

        for (int i = 0; i < p.AChunkCount; i++)
        {
            int startBit = i * p.BlockBits;
            int availableBits = Math.Max(0, aBits - startBit);
            int bitsToRead = Math.Min(p.BlockBits, availableBits);

            fa[i] = ReadBlockAsElement(aWords, startBit, bitsToRead, ctx);
        }

        for (int i = 0; i < p.BChunkCount; i++)
        {
            int startBit = i * p.BlockBits;
            int availableBits = Math.Max(0, bBits - startBit);
            int bitsToRead = Math.Min(p.BlockBits, availableBits);

            fb[i] = ReadBlockAsElement(bWords, startBit, bitsToRead, ctx);
        }

        FftInRing(fa, invert: false, ctx);
        FftInRing(fb, invert: false, ctx);

        for (int i = 0; i < p.TransformLength; i++)
            fa[i] = ctx.Multiply(fa[i], fb[i]);

        FftInRing(fa, invert: true, ctx);

        int convolutionLength = p.AChunkCount + p.BChunkCount - 1;

        uint[] result = new uint[CalculateResultWordCapacity(
            convolutionLength,
            p.BlockBits,
            ctx.ModBits)];

        for (int i = 0; i < convolutionLength; i++)
        {
            long bitShift = (long)i * p.BlockBits;
            AddElementShifted(result, fa[i], bitShift, ctx);
        }

        return TrimWords(result);
    }

    private static void FftInRing(RingElement[] a, bool invert, RingContext ctx)
    {
        int n = a.Length;

        if ((n & (n - 1)) != 0)
            throw new ArgumentException("FFT length must be a power of two.", nameof(a));

        int twoM = ctx.TwoModBits;

        if (twoM % n != 0)
            throw new InvalidOperationException("Transform length must divide 2M.");

        int rootShift = twoM / n;

        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;

            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }

            j ^= bit;

            if (i < j)
                (a[i], a[j]) = (a[j], a[i]);
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            int baseStep = (int)(((long)rootShift * (n / len)) % twoM);
            int step = invert ? ((twoM - baseStep) % twoM) : baseStep;

            int half = len >> 1;

            for (int i = 0; i < n; i += len)
            {
                int shift = 0;

                for (int j = 0; j < half; j++)
                {
                    RingElement u = a[i + j];
                    RingElement v = ctx.MultiplyByPowerOfTwo(a[i + j + half], shift);

                    a[i + j] = ctx.Add(u, v);
                    a[i + j + half] = ctx.Subtract(u, v);

                    shift += step;

                    if (shift >= twoM)
                        shift -= twoM;
                }
            }
        }

        if (invert)
        {
            int logN = Log2PowerOfTwo(n);

            /*
             * n = 2^logN.
             *
             * В кольце Z / (2^M + 1):
             *
             *     2^M ≡ -1
             *     2^(2M) ≡ 1
             *
             * Поэтому обратный элемент к 2^logN:
             *
             *     2^(-logN) ≡ 2^(2M - logN)
             */
            int inverseShift = (twoM - logN) % twoM;

            for (int i = 0; i < n; i++)
                a[i] = ctx.MultiplyByPowerOfTwo(a[i], inverseShift);
        }
    }

    private static SsParameters ChooseParameters(int aBits, int bBits)
    {
        int n = MinTransformLength;

        while (true)
        {
            int ringBits = n;
            int logN = Log2PowerOfTwo(n);

            /*
             * Каждый коэффициент после свёртки ограничен примерно:
             *
             *     n * (2^blockBits - 1)^2
             *
             * Нужно, чтобы он точно помещался в модуль:
             *
             *     2^ringBits + 1
             *
             * Поэтому берём:
             *
             *     2 * blockBits + log2(n) + запас <= ringBits
             */
            int blockBits = (ringBits - logN - GuardBits) / 2;

            if (blockBits > 0)
            {
                long aChunks = ((long)aBits + blockBits - 1) / blockBits;
                long bChunks = ((long)bBits + blockBits - 1) / blockBits;
                long convolutionLength = aChunks + bChunks - 1;

                if (convolutionLength <= n)
                {
                    if (aChunks > int.MaxValue || bChunks > int.MaxValue)
                        throw new InvalidOperationException("Too many chunks.");

                    return new SsParameters(
                        transformLength: n,
                        ringBits: ringBits,
                        blockBits: blockBits,
                        aChunkCount: (int)aChunks,
                        bChunkCount: (int)bChunks);
                }
            }

            if (n > (1 << 28))
                throw new InvalidOperationException("Input is too large for this implementation.");

            n <<= 1;
        }
    }

    private static RingElement ReadBlockAsElement(
        ReadOnlySpan<uint> source,
        int startBit,
        int bitCount,
        RingContext ctx)
    {
        uint[] raw = new uint[ctx.ValueWordCount];

        int copied = 0;
        int dstIndex = 0;

        while (copied < bitCount)
        {
            int absoluteBit = startBit + copied;
            int sourceWordIndex = absoluteBit >> 5;
            int sourceBitOffset = absoluteBit & 31;

            uint value = 0;

            if (sourceWordIndex < source.Length)
            {
                value = source[sourceWordIndex] >> sourceBitOffset;

                if (sourceBitOffset != 0 && sourceWordIndex + 1 < source.Length)
                    value |= source[sourceWordIndex + 1] << (32 - sourceBitOffset);
            }

            int take = Math.Min(32, bitCount - copied);

            if (take < 32)
                value &= (1u << take) - 1u;

            raw[dstIndex++] = value;
            copied += take;
        }

        return ctx.FromRaw(raw);
    }

    private static void AddElementShifted(
        uint[] target,
        RingElement value,
        long bitShift,
        RingContext ctx)
    {
        if (ctx.HasModulusTopBit(value))
        {
            throw new InvalidOperationException(
                "Convolution coefficient overflowed the Schönhage-Strassen modulus.");
        }

        int wordShift = (int)(bitShift >> 5);
        int bitOffset = (int)(bitShift & 31);

        uint[] words = value.Words;

        ulong shiftCarry = 0;

        for (int i = 0; i < ctx.LowWordCount; i++)
        {
            uint word = words[i];

            if (i == ctx.LowWordCount - 1)
                word &= ctx.LastLowWordMask;

            ulong shifted = ((ulong)word << bitOffset) | shiftCarry;

            uint low = (uint)shifted;

            if (low != 0)
                AddUInt32(target, wordShift + i, low);

            shiftCarry = shifted >> 32;
        }

        if (shiftCarry != 0)
            AddUInt32(target, wordShift + ctx.LowWordCount, (uint)shiftCarry);
    }

    private static void AddUInt32(uint[] target, int index, uint value)
    {
        ulong carry = value;

        while (carry != 0)
        {
            if (index >= target.Length)
                throw new InvalidOperationException("Result buffer is too small.");

            ulong sum = (ulong)target[index] + carry;

            target[index] = (uint)sum;
            carry = sum >> 32;

            index++;
        }
    }

    private static int CalculateResultWordCapacity(
        int convolutionLength,
        int blockBits,
        int ringBits)
    {
        long maxBits = (long)convolutionLength * blockBits + ringBits + 64L;
        long wordCount = (maxBits + 31) / 32 + 4;

        if (wordCount > int.MaxValue)
            throw new InvalidOperationException("Result is too large.");

        return (int)wordCount;
    }

    private static int BitLength(ReadOnlySpan<uint> digits)
    {
        int len = TrimmedLength(digits);

        if (len == 0)
            return 0;

        uint top = digits[len - 1];

        int topBits = 0;

        while (top != 0)
        {
            topBits++;
            top >>= 1;
        }

        return (len - 1) * 32 + topBits;
    }

    private static int TrimmedLength(ReadOnlySpan<uint> digits)
    {
        int len = digits.Length;

        while (len > 0 && digits[len - 1] == 0)
            len--;

        return len;
    }

    private static uint[] TrimWords(ReadOnlySpan<uint> digits)
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

    private static int Log2PowerOfTwo(int value)
    {
        int result = 0;

        while (value > 1)
        {
            value >>= 1;
            result++;
        }

        return result;
    }

    private readonly struct SsParameters
    {
        public readonly int TransformLength;
        public readonly int RingBits;
        public readonly int BlockBits;
        public readonly int AChunkCount;
        public readonly int BChunkCount;

        public SsParameters(
            int transformLength,
            int ringBits,
            int blockBits,
            int aChunkCount,
            int bChunkCount)
        {
            TransformLength = transformLength;
            RingBits = ringBits;
            BlockBits = blockBits;
            AChunkCount = aChunkCount;
            BChunkCount = bChunkCount;
        }
    }

    private sealed class RingElement
    {
        public readonly uint[] Words;

        public RingElement(uint[] words)
        {
            Words = words;
        }
    }

    private sealed class RingContext
    {
        public readonly int ModBits;
        public readonly int TwoModBits;
        public readonly int ValueWordCount;
        public readonly int LowWordCount;
        public readonly uint LastLowWordMask;
        public readonly RingElement Zero;

        private readonly uint[] _modulus;
        private readonly int _topWordIndex;
        private readonly int _topBitOffset;

        public RingContext(int modBits)
        {
            if (modBits <= 0)
                throw new ArgumentOutOfRangeException(nameof(modBits));

            ModBits = modBits;
            TwoModBits = modBits * 2;

            _topWordIndex = modBits >> 5;
            _topBitOffset = modBits & 31;

            ValueWordCount = _topWordIndex + 1;
            LowWordCount = (modBits + 31) / 32;

            if ((modBits & 31) == 0)
                LastLowWordMask = uint.MaxValue;
            else
                LastLowWordMask = (1u << (modBits & 31)) - 1u;

            _modulus = new uint[ValueWordCount];
            _modulus[0] = 1u;
            _modulus[_topWordIndex] |= 1u << _topBitOffset;

            Zero = new RingElement(new uint[ValueWordCount]);
        }

        public RingElement FromRaw(uint[] raw)
        {
            return new RingElement(ReduceToFixed(raw));
        }

        public RingElement Add(RingElement left, RingElement right)
        {
            uint[] sum = AddArrays(left.Words, right.Words);
            return new RingElement(ReduceToFixed(sum));
        }

        public RingElement Subtract(RingElement left, RingElement right)
        {
            int cmp = Compare(left.Words, right.Words);

            if (cmp == 0)
                return Zero;

            if (cmp > 0)
            {
                uint[] difference = SubtractArrays(left.Words, right.Words);
                return new RingElement(ReduceToFixed(difference));
            }
            else
            {
                uint[] difference = SubtractArrays(right.Words, left.Words);
                uint[] wrapped = SubtractArrays(_modulus, difference);

                return new RingElement(ReduceToFixed(wrapped));
            }
        }

        public RingElement Multiply(RingElement left, RingElement right)
        {
            if (IsZero(left.Words) || IsZero(right.Words))
                return Zero;

            uint[] product = MultiplyArrays(left.Words, right.Words);
            return new RingElement(ReduceToFixed(product));
        }

        public RingElement MultiplyByPowerOfTwo(RingElement value, int shift)
        {
            if (IsZero(value.Words))
                return Zero;

            shift %= TwoModBits;

            if (shift < 0)
                shift += TwoModBits;

            if (shift == 0)
                return value;

            uint[] shifted = ShiftLeftBits(value.Words, shift);
            return new RingElement(ReduceToFixed(shifted));
        }

        public bool HasModulusTopBit(RingElement value)
        {
            return (value.Words[_topWordIndex] & (1u << _topBitOffset)) != 0;
        }

        private uint[] ReduceToFixed(uint[] value)
        {
            uint[] reduced = ReduceToVariable(value);

            uint[] fixedValue = new uint[ValueWordCount];

            int copyLength = Math.Min(reduced.Length, fixedValue.Length);

            Array.Copy(reduced, fixedValue, copyLength);

            MaskBitsAboveModBit(fixedValue);

            return fixedValue;
        }

        private uint[] ReduceToVariable(uint[] value)
        {
            value = Trim(value);

            if (value.Length == 0)
                return Array.Empty<uint>();

            int cmp = Compare(value, _modulus);

            if (cmp < 0)
                return value;

            if (cmp == 0)
                return Array.Empty<uint>();

            /*
             * Основное свойство кольца:
             *
             *     2^M ≡ -1 mod (2^M + 1)
             *
             * Любое число X можно записать:
             *
             *     X = low + high * 2^M
             *
             * Тогда:
             *
             *     X ≡ low - high mod (2^M + 1)
             */
            uint[] low = LowBits(value);
            uint[] high = ShiftRightBits(value, ModBits);

            high = ReduceToVariable(high);

            int lowHighCmp = Compare(low, high);

            if (lowHighCmp == 0)
                return Array.Empty<uint>();

            if (lowHighCmp > 0)
            {
                return SubtractArrays(low, high);
            }
            else
            {
                uint[] difference = SubtractArrays(high, low);
                uint[] wrapped = SubtractArrays(_modulus, difference);

                return Trim(wrapped);
            }
        }

        private uint[] LowBits(uint[] value)
        {
            uint[] result = new uint[LowWordCount];

            int copyLength = Math.Min(value.Length, result.Length);

            Array.Copy(value, result, copyLength);

            if (result.Length > 0)
                result[result.Length - 1] &= LastLowWordMask;

            return Trim(result);
        }

        private void MaskBitsAboveModBit(uint[] value)
        {
            if (value.Length == 0)
                return;

            uint mask;

            if (_topBitOffset == 31)
                mask = uint.MaxValue;
            else
                mask = (1u << (_topBitOffset + 1)) - 1u;

            value[_topWordIndex] &= mask;

            for (int i = _topWordIndex + 1; i < value.Length; i++)
                value[i] = 0;
        }

        private static uint[] AddArrays(uint[] left, uint[] right)
        {
            int max = Math.Max(left.Length, right.Length);

            uint[] result = new uint[max + 1];

            ulong carry = 0;

            for (int i = 0; i < max; i++)
            {
                ulong a = i < left.Length ? left[i] : 0UL;
                ulong b = i < right.Length ? right[i] : 0UL;

                ulong sum = a + b + carry;

                result[i] = (uint)sum;
                carry = sum >> 32;
            }

            if (carry != 0)
                result[max] = (uint)carry;

            return Trim(result);
        }

        private static uint[] SubtractArrays(uint[] left, uint[] right)
        {
            uint[] result = new uint[left.Length];

            long borrow = 0;

            for (int i = 0; i < left.Length; i++)
            {
                long a = left[i];
                long b = i < right.Length ? right[i] : 0L;

                long current = a - b - borrow;

                if (current < 0)
                {
                    current += 1L << 32;
                    borrow = 1;
                }
                else
                {
                    borrow = 0;
                }

                result[i] = (uint)current;
            }

            return Trim(result);
        }

        private static uint[] MultiplyArrays(uint[] left, uint[] right)
        {
            left = Trim(left);
            right = Trim(right);

            if (left.Length == 0 || right.Length == 0)
                return Array.Empty<uint>();

            uint[] result = new uint[left.Length + right.Length + 1];

            for (int i = 0; i < left.Length; i++)
            {
                ulong carry = 0;

                for (int j = 0; j < right.Length; j++)
                {
                    ulong current =
                        result[i + j] +
                        (ulong)left[i] * right[j] +
                        carry;

                    result[i + j] = (uint)current;
                    carry = current >> 32;
                }

                int k = i + right.Length;

                while (carry != 0)
                {
                    ulong current = result[k] + carry;

                    result[k] = (uint)current;
                    carry = current >> 32;

                    k++;
                }
            }

            return Trim(result);
        }

        private static uint[] ShiftLeftBits(uint[] value, int shift)
        {
            value = Trim(value);

            if (value.Length == 0)
                return Array.Empty<uint>();

            int wordShift = shift >> 5;
            int bitShift = shift & 31;

            uint[] result = new uint[value.Length + wordShift + 1];

            if (bitShift == 0)
            {
                Array.Copy(value, 0, result, wordShift, value.Length);
                return Trim(result);
            }

            ulong carry = 0;

            for (int i = 0; i < value.Length; i++)
            {
                ulong current = ((ulong)value[i] << bitShift) | carry;

                result[i + wordShift] = (uint)current;
                carry = current >> 32;
            }

            if (carry != 0)
                result[value.Length + wordShift] = (uint)carry;

            return Trim(result);
        }

        private static uint[] ShiftRightBits(uint[] value, int shift)
        {
            value = Trim(value);

            if (value.Length == 0)
                return Array.Empty<uint>();

            int wordShift = shift >> 5;
            int bitShift = shift & 31;

            if (wordShift >= value.Length)
                return Array.Empty<uint>();

            int newLength = value.Length - wordShift;
            uint[] result = new uint[newLength];

            for (int i = 0; i < newLength; i++)
            {
                uint current = value[i + wordShift] >> bitShift;

                if (bitShift != 0 && i + wordShift + 1 < value.Length)
                    current |= value[i + wordShift + 1] << (32 - bitShift);

                result[i] = current;
            }

            return Trim(result);
        }

        private static int Compare(uint[] left, uint[] right)
        {
            int leftLength = EffectiveLength(left);
            int rightLength = EffectiveLength(right);

            if (leftLength != rightLength)
                return leftLength.CompareTo(rightLength);

            for (int i = leftLength - 1; i >= 0; i--)
            {
                if (left[i] < right[i])
                    return -1;

                if (left[i] > right[i])
                    return 1;
            }

            return 0;
        }

        private static bool IsZero(uint[] value)
        {
            return EffectiveLength(value) == 0;
        }

        private static int EffectiveLength(uint[] value)
        {
            int len = value.Length;

            while (len > 0 && value[len - 1] == 0)
                len--;

            return len;
        }

        private static uint[] Trim(uint[] value)
        {
            int len = EffectiveLength(value);

            if (len == 0)
                return Array.Empty<uint>();

            if (len == value.Length)
                return value;

            uint[] result = new uint[len];

            Array.Copy(value, result, len);

            return result;
        }
    }
}