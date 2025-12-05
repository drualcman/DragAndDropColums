namespace DragAndDropColums.Client.Models;

public class GridLayout
{
    public List<GridItem> Items { get; set; } = new();
    private HashSet<(int row, int col)> occupied = new();

    public void RecalculateOccupied()
    {
        occupied.Clear();
        foreach (var item in Items)
        {
            if (item.IsFullWidth)
            {
                occupied.Add((item.Row, 1));
                occupied.Add((item.Row, 2));
            }
            else
            {
                occupied.Add((item.Row, item.Column));
            }
        }
    }

    public void RemoveFromOccupied(GridItem item)
    {
        if (item.IsFullWidth)
        {
            occupied.Remove((item.Row, 1));
            occupied.Remove((item.Row, 2));
        }
        else
        {
            occupied.Remove((item.Row, item.Column));
        }
    }

    public bool CanPlace(GridItem item)
    {
        if (item.IsFullWidth)
            return !occupied.Contains((item.Row, 1)) && !occupied.Contains((item.Row, 2));

        return item.Column >= 1 && item.Column <= 2 && !occupied.Contains((item.Row, item.Column));
    }

    public void ReorderAll()
    {
        int order = 0;
        foreach (var item in Items.OrderBy(i => i.Row).ThenByDescending(i => i.IsFullWidth ? 1 : 0))
            item.Order = order++;
    }
}
