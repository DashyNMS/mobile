namespace DashyNMS.Mobile.DeviceSections;

/// <summary>Projects Core's depth-first inventory into visible rows, preserving ancestors during search.</summary>
public static class InventoryRows
{
    public static IReadOnlyList<SectionRow> Visible(IReadOnlyList<SectionRow> rows, ISet<long> collapsed, bool showPorts, string term)
    {
        var result = new List<SectionRow>();
        var cursor = 0;
        while (cursor < rows.Count) Walk();
        return result;

        bool Walk()
        {
            var row = rows[cursor++];
            var position = result.Count;
            var hidden = row.IsInventoryPort && !showPorts;
            var expanded = row.InventoryIndex is not { } id || !collapsed.Contains(id);
            var searching = term.Length > 0;
            var hasChildren = false;
            var childMatches = false;
            result.Add(row);
            while (cursor < rows.Count && rows[cursor].Depth > row.Depth)
            {
                hasChildren |= showPorts || !rows[cursor].IsInventoryPort;
                if (!hidden && (expanded || searching)) childMatches |= Walk();
                else Skip();
            }

            if (hidden || (searching && !row.Matches(term) && !childMatches))
            {
                result.RemoveRange(position, result.Count - position);
                return false;
            }

            result[position] = row with { CanExpand = hasChildren, IsExpanded = searching || expanded };
            return true;
        }

        void Skip()
        {
            var depth = rows[cursor++].Depth;
            while (cursor < rows.Count && rows[cursor].Depth > depth) cursor++;
        }
    }
}
