namespace DragAndDropColums.Client.Services;

public class GridCollisionService<TData>
{
    private readonly GridLayout<TData> _layout;

    public GridCollisionService(GridLayout<TData> layout)
    {
        _layout = layout;
    }

    public bool ItemsCollide(GridItem<TData> item1, int col1, int row1, GridItem<TData> item2)
    {
        bool colOverlap = col1 < item2.Column + item2.ColumnSpan &&
                         col1 + item1.ColumnSpan > item2.Column;
        bool rowOverlap = row1 < item2.Row + item2.RowSpan &&
                         row1 + item1.RowSpan > item2.Row;

        return colOverlap && rowOverlap;
    }

    public bool HasCollision(GridItem<TData> item, int col, int row, List<Guid>? ignoreIds = null)
    {
        if (col < 1)
            return true;
        if (row < 1)
            return true;
        if (col + item.ColumnSpan - 1 > _layout.Columns)
            return true;
        if (row + item.RowSpan - 1 > _layout.Rows)
            return true;

        foreach (GridItem<TData> other in _layout.Items)
        {
            if (other.Id == item.Id)
                continue;
            if (ignoreIds != null && ignoreIds.Contains(other.Id))
                continue;

            if (ItemsCollide(item, col, row, other))
            {
                return true;
            }
        }

        return false;
    }

    public List<GridItem<TData>> GetCollisionsAt(GridItem<TData> item, int col, int row)
    {
        List<GridItem<TData>> collisions = new();

        foreach (GridItem<TData> other in _layout.Items)
        {
            if (other.Id == item.Id)
                continue;

            if (ItemsCollide(item, col, row, other))
            {
                collisions.Add(other);
            }
        }

        return collisions;
    }

    public GridItem<TData>? FindItemAtPosition(int col, int row)
    {
        return _layout.Items.FirstOrDefault(item =>
            col >= item.Column && col < item.Column + item.ColumnSpan &&
            row >= item.Row && row < item.Row + item.RowSpan);
    }

    public HashSet<(int, int)> GetItemCells(GridItem<TData> item, int? targetCol = null, int? targetRow = null)
    {
        var cells = new HashSet<(int, int)>();
        int startCol = targetCol ?? item.Column;
        int startRow = targetRow ?? item.Row;

        for (int r = startRow; r < startRow + item.RowSpan; r++)
        {
            for (int c = startCol; c < startCol + item.ColumnSpan; c++)
            {
                cells.Add((c, r));
            }
        }

        return cells;
    }
}
