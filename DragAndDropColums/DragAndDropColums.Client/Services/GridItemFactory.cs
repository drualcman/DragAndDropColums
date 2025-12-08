namespace DragAndDropColums.Client.Services;

public class GridItemFactory
{
    private static readonly string[] Colors = new[]
    {
        "#FFCCCC", "#CCFFCC", "#CCCCFF", "#FFFFCC", "#FFCCFF", "#CCFFFF"
    };

    public static GridItem CreateNewItem(int index)
    {
        return new GridItem
        {
            Content = $"Elemento {index}",
            Column = 1,
            Row = 1,
            ColumnSpan = 2,
            RowSpan = 2,
            BackgroundColor = Colors[(index - 1) % Colors.Length]
        };
    }
}
