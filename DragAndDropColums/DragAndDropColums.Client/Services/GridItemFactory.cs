namespace DragAndDropColums.Client.Services;

public static class GridItemFactory<TData>
{
    private static readonly string[] Colors =
    {
        "#3498db", "#2ecc71", "#e74c3c", "#f39c12",
        "#9b59b6", "#1abc9c", "#d35400", "#34495e"
    };

    public static GridItem<TData> CreateNewItem(int index)
    {
        var random = new Random();

        return new GridItem<TData>
        {
            Id = Guid.NewGuid(),
            Data = default!,
            Column = 1,
            Row = index,
            ColumnSpan = 2,
            RowSpan = 2,
            BackgroundColor = Colors[random.Next(Colors.Length)]
        };
    }
}
