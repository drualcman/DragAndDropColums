using Microsoft.AspNetCore.Components.Web;
using System.Text.Json;

namespace DragAndDropColums.Client.Components;

public partial class DraggableGrid
{
    [Parameter]
    public GridLayout Layout { get; set; } = new();

    [Parameter]
    public EventCallback<GridLayout> LayoutChanged { get; set; }

    private GridItem? SelectedItem { get; set; }
    private GridItem? DraggingItem { get; set; }
    private (int Col, int Row)? DropTarget { get; set; }
    private bool IsDragging { get; set; }
    private string? DragCursorClass { get; set; }

    private (int StartX, int StartY, int StartCol, int StartRow)? DragStartInfo { get; set; }

    protected override void OnInitialized()
    {
        if (Layout.Items.Any() && SelectedItem == null)
        {
            SelectedItem = Layout.Items.First();
        }
    }

    private string GetGridStyle()
    {
        return $"display: grid; " +
               $"grid-template-columns: repeat({Layout.Columns}, {Layout.CellSize}px); " +
               $"grid-template-rows: repeat({Layout.Rows}, {Layout.CellSize}px); " +
               $"gap: {Layout.Gap}px; " +
               $"width: {Layout.Columns * (Layout.CellSize + Layout.Gap)}px; " +
               $"min-height: {Layout.Rows * (Layout.CellSize + Layout.Gap)}px;";
    }

    private string GetItemStyle(GridItem item)
    {
        bool isSelected = SelectedItem?.Id == item.Id;
        bool isDragging = DraggingItem?.Id == item.Id;

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

    private void StartMouseDrag(MouseEventArgs e, GridItem item)
    {
        DraggingItem = item;
        SelectedItem = item;
        IsDragging = true;
        DragCursorClass = "dragging";
        DragStartInfo = ((int)e.ClientX, (int)e.ClientY, item.Column, item.Row);
        StateHasChanged();
    }

    private void HandleMouseMove(MouseEventArgs e)
    {
        if (!IsDragging || DraggingItem == null || !DragStartInfo.HasValue)
            return;

        var (startX, startY, startCol, startRow) = DragStartInfo.Value;
        int deltaX = (int)e.ClientX - startX;
        int deltaY = (int)e.ClientY - startY;

        int cellSizeWithGap = Layout.CellSize + Layout.Gap;

        if (Math.Abs(deltaX) > 5 || Math.Abs(deltaY) > 5)
        {
            int deltaCol = (int)Math.Round((double)deltaX / cellSizeWithGap);
            int deltaRow = (int)Math.Round((double)deltaY / cellSizeWithGap);

            int newCol = Math.Max(1, Math.Min(startCol + deltaCol, Layout.Columns - DraggingItem.ColumnSpan + 1));
            int newRow = Math.Max(1, Math.Min(startRow + deltaRow, Layout.Rows - DraggingItem.RowSpan + 1));

            if (newCol != DropTarget?.Col || newRow != DropTarget?.Row)
            {
                DropTarget = (newCol, newRow);
                StateHasChanged();
            }
        }
    }

    private async Task HandleMouseUp(MouseEventArgs e)
    {
        if (IsDragging && DraggingItem != null && DropTarget.HasValue)
        {
            var (col, row) = DropTarget.Value;
            await PlaceItemWithForce(DraggingItem, col, row);
        }

        CleanUpDrag();
    }

    private void CleanUpDrag()
    {
        IsDragging = false;
        DraggingItem = null;
        DropTarget = null;
        DragStartInfo = null;
        DragCursorClass = null;
        StateHasChanged();
    }

    // ============ NUEVO SISTEMA DE EMPUJE AGGRESIVO ============

    private bool HasCollision(GridItem item, int col, int row, GridItem? ignoreItem = null)
    {
        if (col < 1 || row < 1)
            return true;
        if (col + item.ColumnSpan - 1 > Layout.Columns)
            return true;
        if (row + item.RowSpan - 1 > Layout.Rows)
            return true;

        foreach (var other in Layout.Items)
        {
            if (other.Id == item.Id || (ignoreItem != null && other.Id == ignoreItem.Id))
                continue;

            bool colOverlap = col < other.Column + other.ColumnSpan && col + item.ColumnSpan > other.Column;
            bool rowOverlap = row < other.Row + other.RowSpan && row + item.RowSpan > other.Row;

            if (colOverlap && rowOverlap)
                return true;
        }

        return false;
    }

    private List<GridItem> GetCollisionsAt(GridItem item, int col, int row)
    {
        var collisions = new List<GridItem>();

        foreach (var other in Layout.Items)
        {
            if (other.Id == item.Id)
                continue;

            bool colOverlap = col < other.Column + other.ColumnSpan && col + item.ColumnSpan > other.Column;
            bool rowOverlap = row < other.Row + other.RowSpan && row + item.RowSpan > other.Row;

            if (colOverlap && rowOverlap)
                collisions.Add(other);
        }

        return collisions;
    }

    private async Task PlaceItemWithForce(GridItem item, int targetCol, int targetRow)
    {
        // Ajustar límites
        targetCol = Math.Max(1, Math.Min(targetCol, Layout.Columns - item.ColumnSpan + 1));
        targetRow = Math.Max(1, Math.Min(targetRow, Layout.Rows - item.RowSpan + 1));

        // Si está en la misma posición, no hacer nada
        if (item.Column == targetCol && item.Row == targetRow)
            return;

        // Guardar posición original
        int originalCol = item.Column;
        int originalRow = item.Row;

        // Calcular dirección
        int deltaCol = targetCol - originalCol;
        int deltaRow = targetRow - originalRow;

        // Si no hay colisión, mover directamente
        if (!HasCollision(item, targetCol, targetRow))
        {
            item.Column = targetCol;
            item.Row = targetRow;
            await LayoutChanged.InvokeAsync(Layout);
            StateHasChanged();
            return;
        }

        // Sistema de recolocación agresiva
        bool success = await ForcePlacement(item, targetCol, targetRow, deltaCol, deltaRow);

        if (success)
        {
            await LayoutChanged.InvokeAsync(Layout);
            StateHasChanged();
        }
    }

    private async Task<bool> ForcePlacement(GridItem movingItem, int targetCol, int targetRow, int deltaCol, int deltaRow)
    {
        // Guardar estado original
        var originalPositions = Layout.Items.ToDictionary(i => i.Id, i => (i.Column, i.Row));

        // Lista de todos los items que necesitan ser recolocados
        var itemsToRelocate = new HashSet<GridItem> { movingItem };

        // Encontrar todos los items afectados
        FindAllAffectedItems(movingItem, targetCol, targetRow, itemsToRelocate);

        // Intentar recolocar todos los items
        var tempPositions = new Dictionary<Guid, (int Col, int Row)>();

        foreach (var item in itemsToRelocate)
        {
            tempPositions[item.Id] = (item.Column, item.Row);
        }

        // Para el item que se está moviendo, usar la posición objetivo
        movingItem.Column = targetCol;
        movingItem.Row = targetRow;

        // Para los demás items, intentar encontrar nuevas posiciones
        var otherItems = itemsToRelocate.Where(i => i.Id != movingItem.Id).ToList();

        // Ordenar por proximidad al item movible
        otherItems = otherItems.OrderBy(i =>
            Math.Abs(i.Column - targetCol) + Math.Abs(i.Row - targetRow)).ToList();

        foreach (var otherItem in otherItems)
        {
            // Intentar varias estrategias para recolocar este item
            if (!TryFindNewPositionForItem(otherItem, movingItem, deltaCol, deltaRow))
            {
                // Si no se puede recolocar, revertir todo
                RevertToOriginalPositions(originalPositions);
                return false;
            }
        }

        // Verificar que no haya colisiones después de recolocar
        foreach (var item in Layout.Items)
        {
            if (HasCollision(item, item.Column, item.Row))
            {
                // Hay colisión, revertir
                RevertToOriginalPositions(originalPositions);
                return false;
            }
        }

        return true;
    }

    private void FindAllAffectedItems(GridItem item, int col, int row, HashSet<GridItem> affectedItems)
    {
        var collisions = GetCollisionsAt(item, col, row);

        foreach (var colliding in collisions)
        {
            if (!affectedItems.Contains(colliding))
            {
                affectedItems.Add(colliding);
                // También encontrar items que colisionan con este
                FindAllAffectedItems(colliding, colliding.Column, colliding.Row, affectedItems);
            }
        }
    }

    private bool TryFindNewPositionForItem(GridItem item, GridItem pushingItem, int deltaCol, int deltaRow)
    {
        // Guardar posición original
        int originalCol = item.Column;
        int originalRow = item.Row;

        // Estrategia 1: Intentar mover en la dirección del empuje
        if (deltaCol != 0 || deltaRow != 0)
        {
            // Regla 1: Desplazar solo una celda en la dirección del movimiento
            int newCol = item.Column;
            int newRow = item.Row;

            if (deltaCol != 0)
            {
                newCol = item.Column + Math.Sign(deltaCol);
                newRow = item.Row;
            }
            else if (deltaRow != 0)
            {
                newCol = item.Column;
                newRow = item.Row + Math.Sign(deltaRow);
            }

            // Regla 2: Si toca borde, ir al lado contrario
            if (deltaCol > 0 && newCol + item.ColumnSpan - 1 > Layout.Columns)
            {
                newCol = pushingItem.Column - item.ColumnSpan;
            }
            else if (deltaCol < 0 && newCol < 1)
            {
                newCol = pushingItem.Column + pushingItem.ColumnSpan;
            }
            else if (deltaRow > 0 && newRow + item.RowSpan - 1 > Layout.Rows)
            {
                newRow = pushingItem.Row - item.RowSpan;
            }
            else if (deltaRow < 0 && newRow < 1)
            {
                newRow = pushingItem.Row + pushingItem.RowSpan;
            }

            // Ajustar límites
            newCol = Math.Max(1, Math.Min(newCol, Layout.Columns - item.ColumnSpan + 1));
            newRow = Math.Max(1, Math.Min(newRow, Layout.Rows - item.RowSpan + 1));

            // Verificar si esta posición es válida
            if (!HasCollision(item, newCol, newRow, pushingItem))
            {
                item.Column = newCol;
                item.Row = newRow;
                return true;
            }
        }

        // Estrategia 2: Buscar hacia abajo
        for (int rowOffset = 1; rowOffset <= Layout.Rows; rowOffset++)
        {
            int newRow = originalRow + rowOffset;
            if (newRow + item.RowSpan - 1 <= Layout.Rows &&
                !HasCollision(item, originalCol, newRow, pushingItem))
            {
                item.Column = originalCol;
                item.Row = newRow;
                return true;
            }

            // También probar hacia arriba
            newRow = originalRow - rowOffset;
            if (newRow >= 1 &&
                !HasCollision(item, originalCol, newRow, pushingItem))
            {
                item.Column = originalCol;
                item.Row = newRow;
                return true;
            }
        }

        // Estrategia 3: Buscar hacia la derecha
        for (int colOffset = 1; colOffset <= Layout.Columns; colOffset++)
        {
            int newCol = originalCol + colOffset;
            if (newCol + item.ColumnSpan - 1 <= Layout.Columns &&
                !HasCollision(item, newCol, originalRow, pushingItem))
            {
                item.Column = newCol;
                item.Row = originalRow;
                return true;
            }

            // También probar hacia la izquierda
            newCol = originalCol - colOffset;
            if (newCol >= 1 &&
                !HasCollision(item, newCol, originalRow, pushingItem))
            {
                item.Column = newCol;
                item.Row = originalRow;
                return true;
            }
        }

        // Estrategia 4: Buscar en todas las posiciones posibles (último recurso)
        for (int row = 1; row <= Layout.Rows - item.RowSpan + 1; row++)
        {
            for (int col = 1; col <= Layout.Columns - item.ColumnSpan + 1; col++)
            {
                if (!HasCollision(item, col, row, pushingItem))
                {
                    item.Column = col;
                    item.Row = row;
                    return true;
                }
            }
        }

        // No se encontró ninguna posición válida
        item.Column = originalCol;
        item.Row = originalRow;
        return false;
    }

    private void RevertToOriginalPositions(Dictionary<Guid, (int Column, int Row)> originalPositions)
    {
        foreach (var item in Layout.Items)
        {
            if (originalPositions.TryGetValue(item.Id, out var originalPos))
            {
                item.Column = originalPos.Column;
                item.Row = originalPos.Row;
            }
        }
    }

    // ============ MOVIMIENTO CON TECLADO MEJORADO ============

    private async Task MoveSelectedItem(int deltaCol, int deltaRow)
    {
        if (SelectedItem == null)
            return;

        int newCol = SelectedItem.Column + deltaCol;
        int newRow = SelectedItem.Row + deltaRow;

        await PlaceItemWithForce(SelectedItem, newCol, newRow);
    }

    // ============ MÉTODOS DE INTERFAZ ============

    private async Task SelectItem(GridItem item)
    {
        if (SelectedItem?.Id == item.Id)
            return;

        SelectedItem = item;
        StateHasChanged();
    }

    private async Task CellClicked(int col, int row)
    {
        if (IsDragging)
            return;

        var itemAtCell = FindItemAtCell(col, row);

        if (itemAtCell != null)
        {
            if (SelectedItem?.Id != itemAtCell.Id)
            {
                await SelectItem(itemAtCell);
            }
        }
        else if (SelectedItem != null)
        {
            await PlaceItemWithForce(SelectedItem, col, row);
        }
    }

    private void DeselectItem()
    {
        SelectedItem = null;
        StateHasChanged();
    }

    private GridItem? FindItemAtCell(int col, int row)
    {
        return Layout.Items.FirstOrDefault(item =>
            col >= item.Column && col < item.Column + item.ColumnSpan &&
            row >= item.Row && row < item.Row + item.RowSpan);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowUp":
                await MoveSelectedItem(0, -1);
                break;
            case "ArrowDown":
                await MoveSelectedItem(0, 1);
                break;
            case "ArrowLeft":
                await MoveSelectedItem(-1, 0);
                break;
            case "ArrowRight":
                await MoveSelectedItem(1, 0);
                break;
            case "Delete":
            case "Backspace":
                if (SelectedItem != null)
                    RemoveItem(SelectedItem);
                break;
            case "Escape":
                DeselectItem();
                break;
            case " ":
                if (SelectedItem != null)
                {
                    DeselectItem();
                }
                else if (Layout.Items.Any())
                {
                    SelectedItem = Layout.Items.First();
                    StateHasChanged();
                }
                break;
        }
    }

    private void ResizeItemWidth(GridItem item, int delta)
    {
        int newColSpan = item.ColumnSpan + delta;

        if (newColSpan < 1)
            newColSpan = 1;
        if (newColSpan > Layout.Columns)
            newColSpan = Layout.Columns;
        if (item.Column + newColSpan - 1 > Layout.Columns)
            return;

        int originalColSpan = item.ColumnSpan;
        item.ColumnSpan = newColSpan;

        if (HasCollision(item, item.Column, item.Row))
        {
            item.ColumnSpan = originalColSpan;
            return;
        }

        LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
    }

    private void ResizeItemHeight(GridItem item, int delta)
    {
        int newRowSpan = item.RowSpan + delta;

        if (newRowSpan < 1)
            newRowSpan = 1;
        if (newRowSpan > Layout.Rows)
            newRowSpan = Layout.Rows;
        if (item.Row + newRowSpan - 1 > Layout.Rows)
            return;

        int originalRowSpan = item.RowSpan;
        item.RowSpan = newRowSpan;

        if (HasCollision(item, item.Column, item.Row))
        {
            item.RowSpan = originalRowSpan;
            return;
        }

        LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
    }

    private void AddNewItem()
    {
        var colors = new[] { "#FFCCCC", "#CCFFCC", "#CCCCFF", "#FFFFCC", "#FFCCFF", "#CCFFFF" };

        var newItem = new GridItem
        {
            Content = $"Elemento {Layout.Items.Count + 1}",
            Column = 1,
            Row = 1,
            ColumnSpan = 2,
            RowSpan = 2,
            BackgroundColor = colors[Layout.Items.Count % colors.Length]
        };

        bool placed = false;
        for (int row = 1; row <= Layout.Rows && !placed; row++)
        {
            for (int col = 1; col <= Layout.Columns && !placed; col++)
            {
                if (col + newItem.ColumnSpan - 1 <= Layout.Columns &&
                    row + newItem.RowSpan - 1 <= Layout.Rows &&
                    !HasCollision(newItem, col, row))
                {
                    newItem.Column = col;
                    newItem.Row = row;
                    Layout.Items.Add(newItem);
                    SelectedItem = newItem;
                    placed = true;
                }
            }
        }

        if (!placed)
        {
            newItem.ColumnSpan = 1;
            newItem.RowSpan = 1;

            for (int row = 1; row <= Layout.Rows && !placed; row++)
            {
                for (int col = 1; col <= Layout.Columns && !placed; col++)
                {
                    if (!HasCollision(newItem, col, row))
                    {
                        newItem.Column = col;
                        newItem.Row = row;
                        Layout.Items.Add(newItem);
                        SelectedItem = newItem;
                        placed = true;
                    }
                }
            }
        }

        LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
    }

    private void RemoveItem(GridItem item)
    {
        Layout.Items.Remove(item);
        if (SelectedItem?.Id == item.Id)
        {
            SelectedItem = Layout.Items.FirstOrDefault();
        }
        LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
    }

    private void ResetGrid()
    {
        Layout.Items.Clear();
        LayoutChanged.InvokeAsync(Layout);
        SelectedItem = null;
        StateHasChanged();
    }

    private void SaveLayout()
    {
        var json = JsonSerializer.Serialize(Layout, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine("Layout guardado:");
        Console.WriteLine(json);
    }

    private void CloseEditor()
    {
        SelectedItem = null;
        StateHasChanged();
    }
}