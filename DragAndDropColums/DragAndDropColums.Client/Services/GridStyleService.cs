namespace DragAndDropColums.Client.Services;

public class GridStyleService
{
    private readonly GridLayout _layout;

    public GridStyleService(GridLayout layout)
    {
        _layout = layout;
    }

    public string GetGridStyle()
    {
        return "display: grid; " +
               $"grid-template-columns: repeat({_layout.Columns}, {_layout.CellSize}px); " +
               $"grid-template-rows: repeat({_layout.Rows}, {_layout.CellSize}px); " +
               $"gap: {_layout.Gap}px; " +
               $"width: {_layout.Columns * (_layout.CellSize + _layout.Gap)}px; " +
               $"min-height: {_layout.Rows * (_layout.CellSize + _layout.Gap)}px;";
    }

    public string GetItemStyle(GridItem item, bool isSelected, bool isDragging)
    {
        string borderColor = isSelected ? "#007bff" :
                             isDragging ? "#ff6b6b" : "#333";
        string borderStyle = isSelected ? "3px solid" :
                             isDragging ? "3px dashed" : "2px solid";
        string boxShadow = isSelected ? "0 0 0 3px rgba(0, 123, 255, 0.3)" : "none";

        return $"grid-column: {item.Column} / span {item.ColumnSpan}; " +
               $"grid-row: {item.Row} / span {item.RowSpan}; " +
               $"background-color: {item.BackgroundColor}; " +
               $"border: {borderStyle} {borderColor}; " +
               $"box-shadow: {boxShadow}; " +
               $"z-index: {(isSelected || isDragging ? "100" : "10")};" +
               $"{(isDragging ? "opacity: 0.8; cursor: grabbing;" : "cursor: grab;")}";
    }

    public bool IsDropAreaCorner(int col, int row, GridItem item, (int Col, int Row) target)
    {
        return (col == target.Col && row == target.Row) || // Esquina superior izquierda
               (col == target.Col + item.ColumnSpan - 1 && row == target.Row) || // Esquina superior derecha
               (col == target.Col && row == target.Row + item.RowSpan - 1) || // Esquina inferior izquierda
               (col == target.Col + item.ColumnSpan - 1 && row == target.Row + item.RowSpan - 1); // Esquina inferior derecha
    }
}
