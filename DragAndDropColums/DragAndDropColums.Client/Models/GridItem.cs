namespace DragAndDropColums.Client.Models;

public class GridItem<TData>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TData Data { get; set; }
    public int Column { get; set; } = 1;
    public int Row { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public string BackgroundColor { get; set; } = "#e0e0e0";
}
