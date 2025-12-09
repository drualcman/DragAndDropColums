namespace DragAndDropColums.Client.Services;

public static class GridItemFactory
{
    public static GridItem CreateNewItem(int index)
    {
        var random = new Random();

        return new GridItem
        {
            Id = Guid.NewGuid(),
            Data = default!,
            Column = 1,
            Row = index,
            ColumnSpan = 2,
            RowSpan = 2
        };
    }
}
