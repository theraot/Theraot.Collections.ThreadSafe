using System;

namespace Theraot.Core
{
    public static class NumericHelper
    {
        [global::System.Diagnostics.DebuggerNonUserCode]
        public static int Log2(int number)
        {
            if (number == 0)
            {
                throw new ArgumentException("The logarithm of zero is not defined.");
            }
            if (number <= 0)
            {
                throw new ArgumentException("The logarithm of a negative number is imaginary.");
            }
            number |= number >> 1;
            number |= number >> 2;
            number |= number >> 4;
            number |= number >> 8;
            number |= number >> 16;
            return PopulationCount(number >> 1);
        }

        [global::System.Diagnostics.DebuggerNonUserCode]
        public static int NextPowerOf2(int number)
        {
            if (number < 0)
            {
                return 1;
            }
            else
            {
                uint _number;
                unchecked
                {
                    _number = (uint)number;
                }
                return (int)NextPowerOf2(_number);
            }
        }

        public static int PopulationCount(int value)
        {
            unchecked
            {
                return PopulationCount((uint)value);
            }
        }

        private static uint NextPowerOf2(uint number)
        {
            number |= number >> 1;
            number |= number >> 2;
            number |= number >> 4;
            number |= number >> 8;
            number |= number >> 16;
            return number + 1;
        }

        private static int PopulationCount(uint value)
        {
            value = value - ((value >> 1) & 0x55555555);
            value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
            value = (value + (value >> 4)) & 0x0F0F0F0F;
            value = value + (value >> 8);
            value = value + (value >> 16);
            return (int)(value & 0x0000003F);
        }
    }
}