using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;
using System.Runtime.InteropServices.Marshalling;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger
{
    private int _signBit;
    
    private uint _smallValue; // Если число маленькое, храним его прямо в этом поле, а _data == null.
    private uint[]? _data;
    
    public bool IsNegative => _signBit == 1;

    /// От массива цифр (little endian)
    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        if (digits == null || digits.Length == 0)
        {
            _data = null;
            _signBit = 0;
            _smallValue = 0;
        }
        else
        {
            int lead_zeros_amo = 0;
            while (digits.Length - 1 - lead_zeros_amo >= 0 && digits[digits.Length - 1 - lead_zeros_amo] == 0)
            {
                lead_zeros_amo++;
            }

            if (lead_zeros_amo >= digits.Length)
            {
                _data = null;
                _signBit = 0;
                _smallValue = 0;
            }
            else if (lead_zeros_amo == digits.Length - 1)
            {
                _data = null;
                _signBit = isNegative ? 1 : 0;
                _smallValue = digits[0];
            }
            else
            {
                _smallValue = 0;
                _signBit = isNegative ? 1 : 0;
                _data = new uint[digits.Length - lead_zeros_amo];
                for (int i = 0; i < digits.Length - lead_zeros_amo; i++)
                {
                    _data[i] = digits[i];
                }
            }
        }
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

    private void InitializeFromDigits(uint[] digits, bool isNegative)
    {
        if (digits == null || digits.Length == 0)
        {
            _data = null;
            _signBit = 0;
            _smallValue = 0;
            return;
        }

        int leadZerosAmount = 0;
        while (digits.Length - 1 - leadZerosAmount >= 0 &&
               digits[digits.Length - 1 - leadZerosAmount] == 0)
        {
            leadZerosAmount++;
        }

        int actualLength = digits.Length - leadZerosAmount;

        if (actualLength == 0)
        {
            _data = null;
            _signBit = 0;
            _smallValue = 0;
        }
        else if (actualLength == 1)
        {
            _data = null;
            _signBit = isNegative ? 1 : 0;
            _smallValue = digits[0];
        }
        else
        {
            _data = new uint[actualLength];
            Array.Copy(digits, _data, actualLength);
            _smallValue = 0;
            _signBit = isNegative ? 1 : 0;
        }
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

        ulong carry = 0;

        for (int i = 0; i < digits.Count; i++)
        {
            ulong cur = (ulong)digits[i] * multiplier + carry;
            digits[i] = (uint)cur;
            carry = cur >> 32;
        }

        if (carry != 0)
            digits.Add((uint)carry);
    }

    private static void AddSmall(List<uint> digits, uint value)
    {
        ulong carry = value;
        int i = 0;

        while (carry != 0 && i < digits.Count)
        {
            ulong cur = (ulong)digits[i] + carry;
            digits[i] = (uint)cur;
            carry = cur >> 32;
            i++;
        }

        if (carry != 0)
            digits.Add((uint)carry);
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


    public ReadOnlySpan<uint> GetDigits()
    {
        return _data ?? [_smallValue];
    }
    
    public int CompareTo(IBigInteger? other) => throw new NotImplementedException();
    public bool Equals(IBigInteger? other) => throw new NotImplementedException();
    public override bool Equals(object? obj) => obj is IBigInteger other && Equals(other);
    public override int GetHashCode() => throw new NotImplementedException();
    
    
    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b) => throw new NotImplementedException();
    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b) => throw new NotImplementedException();
    public static BetterBigInteger operator -(BetterBigInteger a) => throw new NotImplementedException();
    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b) => throw new NotImplementedException();
    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b) => throw new NotImplementedException();
    
    
    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
       => throw new NotImplementedException("Умножение делегируется стратегии, выбирать необходимо в зависимости от размеров чисел");
    
    public static BetterBigInteger operator ~(BetterBigInteger a) => throw new NotImplementedException();
    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b) => throw new NotImplementedException();
    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b) => throw new NotImplementedException();
    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b) => throw new NotImplementedException();
    public static BetterBigInteger operator <<(BetterBigInteger a, int shift) => throw new NotImplementedException();
    public static BetterBigInteger operator >> (BetterBigInteger a, int shift) => throw new NotImplementedException();
    
    public static bool operator ==(BetterBigInteger a, BetterBigInteger b) => Equals(a, b);
    public static bool operator !=(BetterBigInteger a, BetterBigInteger b) => !Equals(a, b);
    public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) < 0;
    public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) > 0;
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) >= 0;
    
    public override string ToString() => ToString(10);
    public string ToString(int radix) => throw new NotImplementedException();
    
}