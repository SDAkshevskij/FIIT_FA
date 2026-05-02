using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;
using System.Runtime.InteropServices.Marshalling;
using System.Text;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger
{
    private int _signBit;
    
    private uint _smallValue; // Если число маленькое, храним его прямо в этом поле, а _data == null.
    private uint[]? _data;

    private static readonly IMultiplier _simpleMultiplier = new SimpleMultiplier();
    private static readonly IMultiplier _karatsubaMultiplier = new KaratsubaMultiplier();
    private static readonly IMultiplier _fftMultiplier = new FftMultiplier();

    private const int KaratsubaThreshold = 32;
    private const int FftThreshold = 256;
    private const int BitsPerByte = 8;
    private const int UIntBits = sizeof(uint) * BitsPerByte;
    private const int HalfUIntBits = UIntBits / 2;
    private const uint HalfMask = (1u << HalfUIntBits) - 1u;
    private const int UIntBitIndexMask = UIntBits - 1;
    private const uint UIntHighBitMask = 1u << (UIntBits - 1);

    public bool IsNegative => _signBit == 1;

    /// От массива цифр (little endian)
    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        InitializeFromDigits(digits, isNegative);
    }

    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
        : this(digits?.ToArray() ?? Array.Empty<uint>(), isNegative)
    {
    }

    public BetterBigInteger(string value, int radix)
    {
        var parsed = ParseStringToDigits(value, radix);

        InitializeFromDigits(parsed.digits, parsed.isNegative);
    }

    private void InitializeFromDigits(uint[]? digits, bool isNegative)
    {
        if (digits == null || digits.Length == 0)
        {
            _data = null;
            _smallValue = 0;
            _signBit = 0;
            return;
        }

        int actualLength = digits.Length;
        while (actualLength > 0 && digits[actualLength - 1] == 0)
        {
            actualLength--;
        }

        if (actualLength == 0)
        {
            _data = null;
            _smallValue = 0;
            _signBit = 0;
            return;
        }

        if (actualLength == 1)
        {
            _data = null;
            _smallValue = digits[0];
            _signBit = isNegative ? 1 : 0;
            return;
        }

        _data = new uint[actualLength];
        Array.Copy(digits, _data, actualLength);
        _smallValue = 0;
        _signBit = isNegative ? 1 : 0;
    }



    private static (uint[] digits, bool isNegative) ParseStringToDigits(string value, int radix)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        if (radix < 2 || radix > 36)
            throw new ArgumentOutOfRangeException(nameof(radix), "radix must be in range [2, 36]");

        value = value.Trim();
        if (value.Length == 0)
            throw new FormatException("Empty string is not a valid integer.");

        bool isNegative = false;
        int start = 0;

        if (value[0] == '+' || value[0] == '-')
        {
            isNegative = value[0] == '-';
            start = 1;
        }

        if (start == value.Length)
            throw new FormatException("String contains only sign without digits.");

        List<uint> digits = new List<uint> { 0 };

        for (int i = start; i < value.Length; i++)
        {
            uint digit = ParseDigit(value[i], radix);

            MultiplyBySmall(digits, (uint)radix);
            AddSmall(digits, digit);
        }

        TrimLeadingZeros(digits);

        bool isZero = digits.Count == 1 && digits[0] == 0;
        if (isZero)
            isNegative = false;

        return (digits.ToArray(), isNegative);
    }

    private static uint ParseDigit(char c, int radix)
    {
        int value;

        if (c >= '0' && c <= '9')
            value = c - '0';
        else if (c >= 'A' && c <= 'Z')
            value = c - 'A' + 10;
        else if (c >= 'a' && c <= 'z')
            value = c - 'a' + 10;
        else
            throw new FormatException($"Invalid character {c}");

        if (value >= radix)
            throw new FormatException($"Digit {c} is not valid");

        return (uint)value;
    }

    private static void MultiplyBySmall(List<uint> digits, uint multiplier)
    {
        if (multiplier == 0)
        {
            digits.Clear();
            digits.Add(0);
            return;
        }

        if (multiplier == 1)
            return;

        int len = digits.Count;

        uint[] result = new uint[len + 1];

        for (int i = 0; i < len; i++)
        {
            AddUIntProductByHalves(
                result,
                i,
                digits[i],
                multiplier);
        }

        int actualLength = TrimmedLength(result);

        digits.Clear();

        if (actualLength == 0)
        {
            digits.Add(0);
            return;
        }

        for (int i = 0; i < actualLength; i++)
            digits.Add(result[i]);
    }

    private static void AddSmall(List<uint> digits, uint value)
    {
        if (value == 0)
            return;

        int index = 0;
        uint carry = value;

        while (carry != 0)
        {
            if (index == digits.Count)
            {
                digits.Add(carry);
                return;
            }

            uint old = digits[index];
            uint sum = old + carry;

            digits[index] = sum;

            carry = sum < old ? 1u : 0u;
            index++;
        }
    }

    private static void TrimLeadingZeros(List<uint> digits)
    {
        int i = digits.Count - 1;
        while (i > 0 && digits[i] == 0)
        {
            digits.RemoveAt(i);
            i--;
        }

        if (digits.Count == 0)
            digits.Add(0);
    }

    private static uint[] TrimLeadingZeros(ReadOnlySpan<uint> digits)
    {
        int len = TrimmedLength(digits);
        if (len == 0)
            return Array.Empty<uint>();

        uint[] result = new uint[len];
        for (int i = 0; i < len; i++)
            result[i] = digits[i];

        return result;
    }

    public ReadOnlySpan<uint> GetDigits()
    {
        return _data ?? [_smallValue];
    }

    public int CompareTo(IBigInteger? other)
    {
        if (other is null)
            return 1;

        bool thisIsZero = IsZero(this);
        bool otherIsZero = IsZeroDigits(other.GetDigits());

        if (thisIsZero && otherIsZero)
            return 0;

        if (IsNegative != other.IsNegative)
            return IsNegative ? -1 : 1;

        int cmp = CompareMagnitudes(GetDigits(), other.GetDigits());

        return IsNegative ? -cmp : cmp;
    }

    public bool Equals(IBigInteger? other)
    {
        if (other is null)
            return false;

        bool thisIsZero = IsZero(this);
        bool otherIsZero = IsZeroDigits(other.GetDigits());

        if (thisIsZero && otherIsZero)
            return true;

        if (IsNegative != other.IsNegative)
            return false;

        return CompareMagnitudes(GetDigits(), other.GetDigits()) == 0;
    }
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        if (obj is null)
            return false;

        if (obj is IBigInteger otherBigInt)
            return Equals(otherBigInt);

        return false;
    }
    public override int GetHashCode()
    {
        if (IsZero(this))
            return 0;

        HashCode hash = new HashCode();
        hash.Add(IsNegative);

        ReadOnlySpan<uint> digits = GetDigits();
        int len = TrimmedLength(digits);

        for (int i = 0; i < len; i++)
        {
            hash.Add(digits[i]);
        }

        return hash.ToHashCode();
    }


    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null || b is null)
            throw new ArgumentNullException();

        ReadOnlySpan<uint> aDigits = a.GetDigits();
        ReadOnlySpan<uint> bDigits = b.GetDigits();

        bool aNeg = a.IsNegative;
        bool bNeg = b.IsNegative;

        if (aNeg == bNeg)
        {
            uint[] sum = AddMagnitudes(aDigits, bDigits);
            return new BetterBigInteger(sum, aNeg);
        }

        int cmp = CompareMagnitudes(aDigits, bDigits);

        if (cmp == 0)
        {
            return new BetterBigInteger(Array.Empty<uint>(), false);
        }

        if (cmp > 0)
        {
            uint[] diff = SubtractMagnitudes(aDigits, bDigits);
            return new BetterBigInteger(diff, aNeg);
        }
        else
        {
            uint[] diff = SubtractMagnitudes(bDigits, aDigits);
            return new BetterBigInteger(diff, bNeg);
        }
    }

    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));
        if (b is null)
            throw new ArgumentNullException(nameof(b));

        return a + (-b);
    }
    public static BetterBigInteger operator -(BetterBigInteger value)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));

        bool isZero = value._data == null && value._smallValue == 0;
        if (isZero)
            return new BetterBigInteger(Array.Empty<uint>(), false);

        if (value._data == null)
            return new BetterBigInteger(new uint[] { value._smallValue }, !value.IsNegative);

        return new BetterBigInteger(value._data, !value.IsNegative);
    }
    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));
        if (b is null)
            throw new ArgumentNullException(nameof(b));

        DivRem(a, b, out BetterBigInteger quotient, out _);
        return quotient;
    }

    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));
        if (b is null)
            throw new ArgumentNullException(nameof(b));

        DivRem(a, b, out _, out BetterBigInteger remainder);
        return remainder;
    }

    private static void DivRem(BetterBigInteger dividend, BetterBigInteger divisor,
        out BetterBigInteger quotient, out BetterBigInteger remainder)
    {
        if (IsZero(divisor))
            throw new DivideByZeroException();

        if (IsZero(dividend))
        {
            quotient = new BetterBigInteger(Array.Empty<uint>(), false);
            remainder = new BetterBigInteger(Array.Empty<uint>(), false);
            return;
        }

        ReadOnlySpan<uint> a = dividend.GetDigits();
        ReadOnlySpan<uint> b = divisor.GetDigits();

        int cmp = CompareMagnitudes(a, b);

        if (cmp < 0)
        {
            quotient = new BetterBigInteger(Array.Empty<uint>(), false);
            remainder = new BetterBigInteger(a.ToArray(), dividend.IsNegative);
            return;
        }

        if (cmp == 0)
        {
            bool qNeg = dividend.IsNegative ^ divisor.IsNegative;
            quotient = new BetterBigInteger(new uint[] { 1 }, qNeg);
            remainder = new BetterBigInteger(Array.Empty<uint>(), false);
            return;
        }

        DivRemMagnitudes(a, b, out uint[] qDigits, out uint[] rDigits);

        bool quotientNegative = !IsZeroDigits(qDigits) && (dividend.IsNegative ^ divisor.IsNegative);
        bool remainderNegative = !IsZeroDigits(rDigits) && dividend.IsNegative;

        quotient = new BetterBigInteger(qDigits, quotientNegative);
        remainder = new BetterBigInteger(rDigits, remainderNegative);
    }

    private static void DivRemMagnitudes(
    ReadOnlySpan<uint> dividend,
    ReadOnlySpan<uint> divisor,
    out uint[] quotient,
    out uint[] remainder)
    {
        int dividendBitLength = GetBitLength(dividend);

        quotient = new uint[(dividendBitLength + UIntBits - 1) / UIntBits];
        remainder = new uint[] { 0 };

        for (int bit = dividendBitLength - 1; bit >= 0; bit--)
        {
            remainder = ShiftLeftOne(remainder);

            if (GetBit(dividend, bit))
                remainder[0] |= 1u;

            if (CompareMagnitudes(remainder, divisor) >= 0)
            {
                remainder = SubtractMagnitudes(remainder, divisor);
                SetBit(quotient, bit);
            }
        }

        quotient = TrimLeadingZeros(quotient);
        remainder = TrimLeadingZeros(remainder);
    }

    private static bool IsZero(BetterBigInteger value)
    {
        return value._data == null && value._smallValue == 0;
    }

    private static bool IsZeroDigits(ReadOnlySpan<uint> digits)
    {
        return TrimmedLength(digits) == 0;
    }

    private static uint[] ShiftLeftOne(ReadOnlySpan<uint> digits)
    {
        int len = TrimmedLength(digits);

        if (len == 0)
            return new uint[] { 0 };

        uint[] result = new uint[len + 1];

        uint carry = 0;

        for (int i = 0; i < len; i++)
        {
            uint current = digits[i];

            result[i] = (current << 1) | carry;

            carry = (current & UIntHighBitMask) != 0 ? 1u : 0u;
        }

        result[len] = carry;

        return TrimLeadingZeros(result);
    }

    private static bool GetBit(ReadOnlySpan<uint> digits, int bitIndex)
    {
        int wordIndex = bitIndex / UIntBits;
        int bitOffset = bitIndex & UIntBitIndexMask;

        if (wordIndex >= digits.Length)
            return false;

        return ((digits[wordIndex] >> bitOffset) & 1u) != 0;
    }

    private static void SetBit(uint[] digits, int bitIndex)
    {
        int wordIndex = bitIndex / UIntBits;
        int bitOffset = bitIndex & UIntBitIndexMask;

        digits[wordIndex] |= 1u << bitOffset;
    }

    private static int GetBitLength(ReadOnlySpan<uint> digits)
    {
        int len = TrimmedLength(digits);

        if (len == 0)
            return 0;

        uint msw = digits[len - 1];
        int bitsInMsw = 0;

        while (msw != 0)
        {
            bitsInMsw++;
            msw >>= 1;
        }

        return (len - 1) * UIntBits + bitsInMsw;
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

        return result;
    }

    private static uint[] SubtractMagnitudes(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        uint[] result = new uint[a.Length];

        uint borrow = 0;

        for (int i = 0; i < a.Length; i++)
        {
            uint av = a[i];
            uint bv = i < b.Length ? b[i] : 0u;

            result[i] = SubtractTwoUInt(av, bv, borrow, out borrow);
        }

        return result;
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

    private static BetterBigInteger Abs(BetterBigInteger value)
    {
        return new BetterBigInteger(value.GetDigits().ToArray(), false);
    }

    private static IMultiplier SelectMultiplier(BetterBigInteger a, BetterBigInteger b)
    {
        int maxLen = Math.Max(a.GetDigits().Length, b.GetDigits().Length);

        if (maxLen >= FftThreshold)
            return _fftMultiplier;

        if (maxLen >= KaratsubaThreshold)
            return _karatsubaMultiplier;

        return _simpleMultiplier;
    }

    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));
        if (b is null)
            throw new ArgumentNullException(nameof(b));

        if (IsZero(a) || IsZero(b))
            return Zero();

        bool resultNegative = a.IsNegative ^ b.IsNegative;

        BetterBigInteger absA = Abs(a);
        BetterBigInteger absB = Abs(b);

        IMultiplier multiplier = SelectMultiplier(absA, absB);
        BetterBigInteger magnitudeResult = multiplier.Multiply(absA, absB);

        if (IsZero(magnitudeResult))
            return Zero();

        return new BetterBigInteger(magnitudeResult.GetDigits().ToArray(), resultNegative);
    }

    

    private static BetterBigInteger Zero()
    {
        return new BetterBigInteger(Array.Empty<uint>(), false);
    }


    private static readonly BetterBigInteger One = new BetterBigInteger(new uint[] { 1 });

    public static BetterBigInteger operator ~(BetterBigInteger a)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));

        return -a - One;
    }
    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));
        if (b is null)
            throw new ArgumentNullException(nameof(b));

        int wordCount = Math.Max(
            1,
            Math.Max(TrimmedLength(a.GetDigits()), TrimmedLength(b.GetDigits())) + 1);

        uint[] aWords = ToTwosComplementWords(a.GetDigits(), a.IsNegative, wordCount);
        uint[] bWords = ToTwosComplementWords(b.GetDigits(), b.IsNegative, wordCount);

        uint[] resultWords = new uint[wordCount];
        for (int i = 0; i < wordCount; i++)
        {
            resultWords[i] = aWords[i] & bWords[i];
        }

        return FromTwosComplementWords(resultWords);
    }
    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
{
    if (a is null)
        throw new ArgumentNullException(nameof(a));
    if (b is null)
        throw new ArgumentNullException(nameof(b));

    int wordCount = Math.Max(
        1,
        Math.Max(TrimmedLength(a.GetDigits()), TrimmedLength(b.GetDigits())) + 1);

    uint[] aWords = ToTwosComplementWords(a.GetDigits(), a.IsNegative, wordCount);
    uint[] bWords = ToTwosComplementWords(b.GetDigits(), b.IsNegative, wordCount);

    uint[] resultWords = new uint[wordCount];
    for (int i = 0; i < wordCount; i++)
    {
        resultWords[i] = aWords[i] | bWords[i];
    }

    return FromTwosComplementWords(resultWords);
}

public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
{
    if (a is null)
        throw new ArgumentNullException(nameof(a));
    if (b is null)
        throw new ArgumentNullException(nameof(b));

    int wordCount = Math.Max(
        1,
        Math.Max(TrimmedLength(a.GetDigits()), TrimmedLength(b.GetDigits())) + 1);

    uint[] aWords = ToTwosComplementWords(a.GetDigits(), a.IsNegative, wordCount);
    uint[] bWords = ToTwosComplementWords(b.GetDigits(), b.IsNegative, wordCount);

    uint[] resultWords = new uint[wordCount];
    for (int i = 0; i < wordCount; i++)
    {
        resultWords[i] = aWords[i] ^ bWords[i];
    }

    return FromTwosComplementWords(resultWords);
}
    public static BetterBigInteger operator <<(BetterBigInteger a, int shift)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));

        if (shift < 0)
            throw new ArgumentOutOfRangeException(nameof(shift));

        if (shift == 0)
            return new BetterBigInteger(a.GetDigits().ToArray(), a.IsNegative);

        if (a._data == null && a._smallValue == 0)
            return new BetterBigInteger(Array.Empty<uint>(), false);

        ReadOnlySpan<uint> digits = a.GetDigits();
        int len = TrimmedLength(digits);

        int wordShift = shift / UIntBits;
        int bitShift = shift & UIntBitIndexMask;

        uint[] result = new uint[len + wordShift + 1];

        if (bitShift == 0)
        {
            for (int i = 0; i < len; i++)
                result[i + wordShift] = digits[i];
        }
        else
        {
            for (int i = 0; i < len; i++)
            {
                uint word = digits[i];

                uint lowPart = word << bitShift;
                uint highPart = word >> (UIntBits - bitShift);

                result[i + wordShift] |= lowPart;

                if (highPart != 0)
                    AddUInt(result, i + wordShift + 1, highPart);
            }
        }

        return new BetterBigInteger(result, a.IsNegative);
    }

    public static BetterBigInteger operator >>(BetterBigInteger a, int shift)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));
        if (shift < 0)
            throw new ArgumentOutOfRangeException(nameof(shift));

        if (shift == 0)
            return new BetterBigInteger(a.GetDigits().ToArray(), a.IsNegative);

        if (a._data == null && a._smallValue == 0)
            return new BetterBigInteger(Array.Empty<uint>(), false);

        return ArithmeticRightShift(a, shift);
    }

    private static BetterBigInteger ArithmeticRightShift(BetterBigInteger value, int shift)
    {
        int wordCount = Math.Max(1, TrimmedLength(value.GetDigits()) + 1);

        uint[] src = ToTwosComplementWords(value.GetDigits(), value.IsNegative, wordCount);
        uint[] dst = new uint[wordCount];

        int wordShift = shift / UIntBits;
        int bitShift = shift & UIntBitIndexMask;

        uint fill = value.IsNegative ? uint.MaxValue : 0u;

        for (int i = 0; i < wordCount; i++)
        {
            int srcIndex = i + wordShift;

            uint low = srcIndex < wordCount ? src[srcIndex] : fill;

            if (bitShift == 0)
            {
                dst[i] = low;
            }
            else
            {
                uint high = srcIndex + 1 < wordCount ? src[srcIndex + 1] : fill;

                dst[i] = (low >> bitShift) | (high << (UIntBits - bitShift));
            }
        }

        return FromTwosComplementWords(dst);
    }

    private static uint[] ToTwosComplementWords(ReadOnlySpan<uint> digits, bool isNegative, int wordCount)
    {
        uint[] result = new uint[wordCount];

        int copyLength = Math.Min(digits.Length, wordCount);
        for (int i = 0; i < copyLength; i++)
        {
            result[i] = digits[i];
        }

        if (!isNegative)
            return result;

        for (int i = 0; i < wordCount; i++)
        {
            result[i] = ~result[i];
        }

        AddOneInPlace(result);
        return result;
    }

    private static BetterBigInteger FromTwosComplementWords(ReadOnlySpan<uint> words)
    {
        bool isNegative =
            words.Length > 0 &&
            (words[words.Length - 1] & UIntHighBitMask) != 0;

        if (!isNegative)
            return new BetterBigInteger(TrimLeadingZeros(words), false);

        uint[] magnitude = new uint[words.Length];

        for (int i = 0; i < words.Length; i++)
            magnitude[i] = ~words[i];

        AddOneInPlace(magnitude);

        return new BetterBigInteger(TrimLeadingZeros(magnitude), true);
    }

    private static void AddOneInPlace(uint[] words)
    {
        AddUInt(words, 0, 1u);
    }

    public static bool operator ==(BetterBigInteger a, BetterBigInteger b) => Equals(a, b);
    public static bool operator !=(BetterBigInteger a, BetterBigInteger b) => !Equals(a, b);
    public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) < 0;
    public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) > 0;
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) >= 0;
    
    public override string ToString() => ToString(10);
    public string ToString(int radix)
    {
        if (radix < 2 || radix > 36)
            throw new ArgumentOutOfRangeException(nameof(radix), "radix must be in range [2, 36]");

        if (_data == null && _smallValue == 0)
            return "0";

        uint[] current = GetDigits().ToArray();
        StringBuilder sb = new StringBuilder();

        while (TrimmedLength(current) > 0)
        {
            current = DivRemByUInt(current, (uint)radix, out uint remainder);
            sb.Append(DigitToChar((int)remainder));
        }

        if (IsNegative)
            sb.Append('-');

        char[] chars = sb.ToString().ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    private static uint[] DivRemByUInt(ReadOnlySpan<uint> digits, uint divisor, out uint remainder)
    {
        if (divisor == 0)
            throw new DivideByZeroException();

        int bitLength = GetBitLength(digits);

        if (bitLength == 0)
        {
            remainder = 0;
            return Array.Empty<uint>();
        }

        uint[] quotient = new uint[(bitLength + UIntBits - 1) / UIntBits];

        uint rem = 0;

        for (int bit = bitLength - 1; bit >= 0; bit--)
        {
            bool inputBit = GetBit(digits, bit);

            rem = ShiftRemainderLeftOneAndAppendBit(rem, inputBit, divisor, out bool subtractDivisor);

            if (subtractDivisor)
                SetBit(quotient, bit);
        }

        remainder = rem;
        return TrimLeadingZeros(quotient);
    }

    private static char DigitToChar(int digit)
    {
        if (digit >= 0 && digit <= 9)
            return (char)('0' + digit);

        return (char)('A' + (digit - 10));
    }

    private static void AddUInt(uint[] target, int index, uint value)
    {
        while (value != 0)
        {
            if (index >= target.Length)
                throw new InvalidOperationException("Buffer overflow.");

            uint old = target[index];
            uint sum = old + value;

            target[index] = sum;

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
        uint sum1 = a + b;
        uint carry1 = sum1 < a ? 1u : 0u;

        uint sum2 = sum1 + carryIn;
        uint carry2 = sum2 < sum1 ? 1u : 0u;

        carryOut = carry1 | carry2;

        return sum2;
    }

    private static uint SubtractTwoUInt(
        uint a,
        uint b,
        uint borrowIn,
        out uint borrowOut)
    {
        uint subtrahend = b + borrowIn;
        uint subtrahendOverflow = subtrahend < b ? 1u : 0u;

        if (subtrahendOverflow != 0)
        {
            borrowOut = 1u;
            return a;
        }

        if (a < subtrahend)
        {
            borrowOut = 1u;
            return a - subtrahend;
        }

        borrowOut = 0u;
        return a - subtrahend;
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

    private static uint ShiftRemainderLeftOneAndAppendBit(
    uint remainder,
    bool bit,
    uint divisor,
    out bool subtractDivisor)
    {
        bool overflow = (remainder & UIntHighBitMask) != 0;

        uint candidate = remainder << 1;

        if (bit)
            candidate |= 1u;

        if (overflow || candidate >= divisor)
        {
            subtractDivisor = true;
            return candidate - divisor;
        }

        subtractDivisor = false;
        return candidate;
    }

    private static void AddShiftedHalfProduct(
        uint[] result,
        int index,
        uint product)
    {
        uint lowPart = product << HalfUIntBits;
        uint highPart = product >> HalfUIntBits;

        if (lowPart != 0)
            AddUInt(result, index, lowPart);

        if (highPart != 0)
            AddUInt(result, index + 1, highPart);
    }

}