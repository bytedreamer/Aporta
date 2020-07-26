using System.Collections;
using System.Linq;
using System.Text;

namespace Aporta.Extensions
{
    public static class Helpers
    {
        public static string ToBitString(this BitArray bits)
        {
            var builder = new StringBuilder();

            for (int index = 0; index < bits.Count; index++)
            {
                char ch = bits[index] ? '1' : '0';
                builder.Append(ch);
            }

            return builder.ToString();
        }
        
        public static BitArray ToBitArray(this string bitString)
        {
            return new BitArray(bitString.Select(bit => bit == '1').ToArray());
        }
    }
}