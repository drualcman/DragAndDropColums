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
            await TryPlaceItem(DraggingItem, col, row);
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

    // ============ SISTEMA SIMPLIFICADO DE EMPUJE ============

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

    private async Task TryPlaceItem(GridItem item, int targetCol, int targetRow)
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

        // Primero intentar mover directamente
        if (!HasCollision(item, targetCol, targetRow))
        {
            item.Column = targetCol;
            item.Row = targetRow;
            await LayoutChanged.InvokeAsync(Layout);
            StateHasChanged();
            return;
        }

        // Si hay colisión, usar el sistema de empuje
        bool success = await PushItemsRecursive(item, targetCol, targetRow, deltaCol, deltaRow);

        if (success)
        {
            await LayoutChanged.InvokeAsync(Layout);
            StateHasChanged();
        }
        else
        {
            // Si no se pudo, intentar mover a la posición más cercana
            await FindAndMoveToClosestPosition(item, targetCol, targetRow);
        }
    }

    private async Task<bool> PushItemsRecursive(GridItem pushingItem, int targetCol, int targetRow, int deltaCol, int deltaRow)
    {
        // Guardar estado original de todos los items
        var originalPositions = new Dictionary<Guid, (int Col, int Row)>();
        foreach (var item in Layout.Items)
        {
            originalPositions[item.Id] = (item.Column, item.Row);
        }

        // Lista de items que ya hemos procesado en este intento
        var processedItems = new HashSet<Guid>();

        // Función recursiva para empujar
        bool RecursivePush(GridItem currentItem, int col, int row, bool isRoot = false)
        {
            // Si ya procesamos este item, evitar recursión infinita
            if (processedItems.Contains(currentItem.Id))
                return false;

            processedItems.Add(currentItem.Id);

            // Verificar colisiones en la nueva posición
            var collisions = GetCollisionsAt(currentItem, col, row);

            // Para cada colisión, intentar empujar ese elemento
            foreach (var collidingItem in collisions)
            {
                // Calcular nueva posición para el elemento colisionante
                int newCol = collidingItem.Column;
                int newRow = collidingItem.Row;

                // Regla 1: Desplazar solo una celda en la dirección del movimiento
                if (isRoot) // Solo el elemento raíz sigue la dirección original
                {
                    if (deltaCol != 0)
                    {
                        newCol = collidingItem.Column + Math.Sign(deltaCol);
                        newRow = collidingItem.Row;
                    }
                    else if (deltaRow != 0)
                    {
                        newCol = collidingItem.Column;
                        newRow = collidingItem.Row + Math.Sign(deltaRow);
                    }
                }
                else
                {
                    // Para elementos empujados, ir hacia abajo o derecha
                    // Primero intentar hacia abajo
                    newRow = collidingItem.Row + 1;
                    newCol = collidingItem.Column;

                    // Si no cabe abajo, intentar a la derecha
                    if (newRow + collidingItem.RowSpan - 1 > Layout.Rows)
                    {
                        newRow = collidingItem.Row;
                        newCol = collidingItem.Column + 1;
                    }
                }

                // Regla 2: Si toca borde, ir al lado contrario
                if (isRoot && deltaCol > 0 && newCol + collidingItem.ColumnSpan - 1 > Layout.Columns)
                {
                    newCol = currentItem.Column - collidingItem.ColumnSpan;
                }
                else if (isRoot && deltaCol < 0 && newCol < 1)
                {
                    newCol = currentItem.Column + currentItem.ColumnSpan;
                }
                else if (isRoot && deltaRow > 0 && newRow + collidingItem.RowSpan - 1 > Layout.Rows)
                {
                    newRow = currentItem.Row - collidingItem.RowSpan;
                }
                else if (isRoot && deltaRow < 0 && newRow < 1)
                {
                    newRow = currentItem.Row + currentItem.RowSpan;
                }

                // Ajustar a límites
                newCol = Math.Max(1, Math.Min(newCol, Layout.Columns - collidingItem.ColumnSpan + 1));
                newRow = Math.Max(1, Math.Min(newRow, Layout.Rows - collidingItem.RowSpan + 1));

                // Intentar empujar recursivamente
                if (!RecursivePush(collidingItem, newCol, newRow, false))
                {
                    return false;
                }
            }

            // Si todos los elementos colisionantes se pudieron mover, mover este
            // Verificar que no haya colisiones ahora
            if (!HasCollision(currentItem, col, row, isRoot ? null : pushingItem))
            {
                currentItem.Column = col;
                currentItem.Row = row;
                return true;
            }

            return false;
        }

        // Intentar el empuje
        bool success = RecursivePush(pushingItem, targetCol, targetRow, true);

        if (!success)
        {
            // Revertir a posiciones originales
            foreach (var item in Layout.Items)
            {
                if (originalPositions.TryGetValue(item.Id, out var originalPos))
                {
                    item.Column = originalPos.Col;
                    item.Row = originalPos.Row;
                }
            }
        }

        return success;
    }

    private async Task FindAndMoveToClosestPosition(GridItem item, int targetCol, int targetRow)
    {
        // Buscar la posición válida más cercana usando BFS
        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int col, int row, int distance)>();

        queue.Enqueue((targetCol, targetRow, 0));
        visited.Add((targetCol, targetRow));

        while (queue.Count > 0)
        {
            var (col, row, distance) = queue.Dequeue();

            // Si esta posición es válida, mover el elemento aquí
            if (!HasCollision(item, col, row))
            {
                item.Column = col;
                item.Row = row;
                await LayoutChanged.InvokeAsync(Layout);
                StateHasChanged();
                return;
            }

            // Agregar posiciones vecinas
            var neighbors = new[]
            {
                (col + 1, row),    // Derecha
                (col - 1, row),    // Izquierda
                (col, row + 1),    // Abajo
                (col, row - 1),    // Arriba
                (col + 1, row + 1), // Diagonal inferior derecha
                (col - 1, row - 1), // Diagonal superior izquierda
                (col + 1, row - 1), // Diagonal superior derecha
                (col - 1, row + 1)  // Diagonal inferior izquierda
            };

            foreach (var (nCol, nRow) in neighbors)
            {
                if (nCol >= 1 && nCol <= Layout.Columns - item.ColumnSpan + 1 &&
                    nRow >= 1 && nRow <= Layout.Rows - item.RowSpan + 1 &&
                    !visited.Contains((nCol, nRow)))
                {
                    visited.Add((nCol, nRow));
                    queue.Enqueue((nCol, nRow, distance + 1));
                }
            }
        }

        // Si no se encontró ninguna posición, dejar en la original
        await LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
    }

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
            await TryPlaceItem(SelectedItem, col, row);
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

    private async Task MoveSelectedItem(int deltaCol, int deltaRow)
    {
        if (SelectedItem == null)
            return;

        int newCol = SelectedItem.Column + deltaCol;
        int newRow = SelectedItem.Row + deltaRow;

        await TryPlaceItem(SelectedItem, newCol, newRow);
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

    // ============ MÉTODOS AUXILIARES ============

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