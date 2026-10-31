namespace DashyNMS.Mobile.Services;

/// <summary>
/// Orders names as people read them, for choosers: case aside, spaces at
/// either end aside, and numbers by their value - "Hall 2" before "Hall 10".
/// </summary>
/// <remarks>
/// The network map's Location and Group lists were ordered by character code
/// (#324), which puts "Hall 10" before "Hall 2" and a name with a stray
/// leading space before all the rest, so they didn't look ordered at all.
/// Done by hand rather than with culture data, so it's the same on every
/// phone and in the tests.
/// </remarks>
public sealed class NaturalOrder : IComparer<string?>
{
    public static NaturalOrder Instance { get; } = new();

    private NaturalOrder()
    {
    }

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var a = x.AsSpan().Trim();
        var b = y.AsSpan().Trim();
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                // Whole numbers, by value: leading noughts aside, the longer is
                // the bigger, else the first digit that differs decides.
                var startA = i;
                var startB = j;
                while (i < a.Length && char.IsAsciiDigit(a[i]))
                {
                    i++;
                }

                while (j < b.Length && char.IsAsciiDigit(b[j]))
                {
                    j++;
                }

                var numberA = a[startA..i].TrimStart('0');
                var numberB = b[startB..j].TrimStart('0');
                if (numberA.Length != numberB.Length)
                {
                    return numberA.Length.CompareTo(numberB.Length);
                }

                var digits = numberA.SequenceCompareTo(numberB);
                if (digits != 0)
                {
                    return digits;
                }

                continue;
            }

            var letters = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
            if (letters != 0)
            {
                return letters;
            }

            i++;
            j++;
        }

        // One runs out first: the shorter comes first; the same but for case
        // or noughts, by character code, so the order is always the same.
        var remaining = (a.Length - i).CompareTo(b.Length - j);
        return remaining != 0 ? remaining : string.CompareOrdinal(x, y);
    }
}
