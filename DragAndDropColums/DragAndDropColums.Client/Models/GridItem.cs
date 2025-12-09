namespace DragAndDropColums.Client.Models;

public class GridItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public object Data { get; set; }
    public int Column { get; set; } = 1;
    public int Row { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;

    // Métodos helper para tipado seguro
    public T GetData<T>() => (T)Data!;
    public bool IsDataOfType<T>() => Data is T;
}
