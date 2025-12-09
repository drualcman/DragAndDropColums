namespace DragAndDropColums.Client.Services;

public class GridStyleService
{
    private readonly GridLayout _layout;
    private readonly GridTheme _theme;

    public GridStyleService(GridLayout layout, GridTheme theme)
    {
        _layout = layout;
        _theme = theme;
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
        string borderColor = isSelected ? _theme.SelectedColor :
                             isDragging ? _theme.DraggingColor : "var(--item-border)";
        string borderStyle = isSelected ? "3px solid" :
                             isDragging ? "3px dashed" : "2px solid";
        string boxShadow = isSelected ? $"0 0 0 3px {_theme.SelectedGlowColor}" :
                             isDragging ? $"0 10px 30px {_theme.DraggingGlowColor}" : "none";

        return $"grid-column: {item.Column} / span {item.ColumnSpan}; " +
               $"grid-row: {item.Row} / span {item.RowSpan}; " +
               $"border: {borderStyle} {borderColor}; " +
               $"box-shadow: {boxShadow}; " +
               $"z-index: {(isSelected || isDragging ? "100" : "10")};" +
               $"{(isDragging ? "opacity: 0.8; cursor: grabbing;" : "cursor: grab;")}";
    }
}