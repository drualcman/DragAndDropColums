namespace DragAndDropColums.Client.Models;

public class GridLayout
{
    public List<GridItem> Items { get; set; } = new();
    private Dictionary<(int, int), GridItem> positionMap = new();

    public event Action? OnLayoutChanged;

    public void RecalculateOccupied()
    {
        positionMap.Clear();
        foreach (var item in Items)
        {
            if (item.IsFullWidth)
            {
                positionMap[(item.Row, 1)] = item;
                positionMap[(item.Row, 2)] = item;
            }
            else
            {
                positionMap[(item.Row, item.Column)] = item;
            }
        }
        OnLayoutChanged?.Invoke();
    }

    public void RemoveFromOccupied(GridItem item)
    {
        if (item.IsFullWidth)
        {
            positionMap.Remove((item.Row, 1));
            positionMap.Remove((item.Row, 2));
        }
        else
        {
            positionMap.Remove((item.Row, item.Column));
        }
    }

    public bool CanPlace(GridItem candidate)
    {
        if (candidate.IsFullWidth)
        {
            return !positionMap.ContainsKey((candidate.Row, 1)) &&
                   !positionMap.ContainsKey((candidate.Row, 2));
        }
        else
        {
            return !positionMap.ContainsKey((candidate.Row, candidate.Column));
        }
    }

    public GridItem? GetItemAt(int row, int column)
    {
        positionMap.TryGetValue((row, column), out var item);
        return item;
    }

    public void ReorderAll()
    {
        // Ordenar por posición
        Items = Items
            .OrderBy(i => i.Row)
            .ThenBy(i => i.Column)
            .ToList();

        // Reasignar órdenes
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].Order = i;
        }

        RecalculateOccupied();
    }

    public void SwapItems(GridItem item1, GridItem item2)
    {
        (item1.Row, item2.Row) = (item2.Row, item1.Row);
        (item1.Column, item2.Column) = (item2.Column, item1.Column);
        ReorderAll();
    }
}
