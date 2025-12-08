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

    // Para trackear la posición inicial del drag
    private (int StartX, int StartY, int StartCol, int StartRow)? DragStartInfo { get; set; }

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

        return $"grid-column: {item.Column} / span {item.ColumnSpan}; " +
               $"grid-row: {item.Row} / span {item.RowSpan}; " +
               $"background-color: {item.BackgroundColor}; " +
               $"border: {(isSelected ? "3px solid #007bff" : isDragging ? "3px dashed #ff6b6b" : "2px solid #333")}; " +
               $"z-index: {(isSelected || isDragging ? "100" : "10")};" +
               $"{(isDragging ? "opacity: 0.7;" : "")}";
    }

    // ============ SIMULACIÓN DE DRAG AND DROP CON MOUSE ============

    private void StartMouseDrag(MouseEventArgs e, GridItem item)
    {
        DraggingItem = item;
        SelectedItem = item;
        IsDragging = true;

        // Guardar posición inicial
        DragStartInfo = ((int)e.ClientX, (int)e.ClientY, item.Column, item.Row);

        StateHasChanged();
    }

    private void HandleMouseMove(MouseEventArgs e)
    {
        if (!IsDragging || DraggingItem == null || !DragStartInfo.HasValue)
            return;

        // Calcular movimiento relativo
        var (startX, startY, startCol, startRow) = DragStartInfo.Value;
        int deltaX = (int)e.ClientX - startX;
        int deltaY = (int)e.ClientY - startY;

        // Convertir movimiento de píxeles a celdas (aproximadamente)
        // Asumimos que cada celda es CellSize + Gap
        int cellSizeWithGap = Layout.CellSize + Layout.Gap;
        int deltaCol = (int)Math.Round((double)deltaX / cellSizeWithGap);
        int deltaRow = (int)Math.Round((double)deltaY / cellSizeWithGap);

        // Nueva posición potencial
        int newCol = startCol + deltaCol;
        int newRow = startRow + deltaRow;

        // Actualizar drop target
        DropTarget = (newCol, newRow);
        StateHasChanged();
    }

    private async Task HandleMouseUp(MouseEventArgs e)
    {
        if (!IsDragging || DraggingItem == null || !DropTarget.HasValue)
        {
            CleanUpDrag();
            return;
        }

        // Intentar colocar el elemento
        var (col, row) = DropTarget.Value;
        await TryPlaceItem(DraggingItem, col, row);

        CleanUpDrag();
    }

    private void CleanUpDrag()
    {
        IsDragging = false;
        DraggingItem = null;
        DropTarget = null;
        DragStartInfo = null;
        StateHasChanged();
    }

    // ============ MÉTODOS DE SELECCIÓN Y MOVIMIENTO ============

    private void SelectItem(GridItem item)
    {
        SelectedItem = item;
        StateHasChanged();
    }

    private void DeselectItem()
    {
        SelectedItem = null;
        StateHasChanged();
    }

    private void CellClicked(int col, int row)
    {
        // Primero buscar si hay un elemento en esta celda
        var itemAtCell = FindItemAtCell(col, row);

        if (itemAtCell != null)
        {
            SelectItem(itemAtCell);
        }
        else if (SelectedItem != null)
        {
            // Mover el elemento seleccionado a esta celda
            _ = TryPlaceItem(SelectedItem, col, row);
        }
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
        if (SelectedItem == null)
            return;

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
                RemoveItem(SelectedItem);
                break;
            case "Escape":
                DeselectItem();
                break;
            case " ":
                // Space para seleccionar/deseleccionar
                if (SelectedItem != null)
                    DeselectItem();
                break;
        }
    }

    // ============ COLISIONES Y POSICIONAMIENTO MEJORADO ============

    private bool HasCollision(GridItem item, int targetCol, int targetRow, bool checkSelf = true)
    {
        // Verificar límites de la grid
        if (targetCol < 1 || targetRow < 1)
            return true;
        if (targetCol + item.ColumnSpan - 1 > Layout.Columns)
            return true;
        if (targetRow + item.RowSpan - 1 > Layout.Rows)
            return true;

        // Verificar colisiones con otros items
        foreach (var otherItem in Layout.Items)
        {
            if (checkSelf && otherItem.Id == item.Id)
                continue;

            // Calcular si hay superposición
            bool colOverlap = targetCol < otherItem.Column + otherItem.ColumnSpan &&
                             targetCol + item.ColumnSpan > otherItem.Column;

            bool rowOverlap = targetRow < otherItem.Row + otherItem.RowSpan &&
                             targetRow + item.RowSpan > otherItem.Row;

            if (colOverlap && rowOverlap)
            {
                return true;
            }
        }

        return false;
    }

    private async Task TryPlaceItem(GridItem item, int targetCol, int targetRow)
    {
        // Ajustar para que quepa en la grid
        targetCol = Math.Max(1, Math.Min(targetCol, Layout.Columns - item.ColumnSpan + 1));
        targetRow = Math.Max(1, Math.Min(targetRow, Layout.Rows - item.RowSpan + 1));

        if (targetCol < 1 || targetRow < 1)
            return;

        // Si es la misma posición, no hacer nada
        if (item.Column == targetCol && item.Row == targetRow)
            return;

        // Verificar si hay colisión
        if (HasCollision(item, targetCol, targetRow))
        {
            // Intentar mover otros items si hay colisión
            if (!await TryMoveCollidingItems(item, targetCol, targetRow))
            {
                // Si no se puede mover, buscar la posición más cercana sin colisión
                if (!FindClosestPosition(item, targetCol, targetRow, out int closestCol, out int closestRow))
                {
                    return; // No se puede colocar
                }

                targetCol = closestCol;
                targetRow = closestRow;
            }
        }

        // Colocar el item
        item.Column = targetCol;
        item.Row = targetRow;
        await LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
    }

    private bool FindClosestPosition(GridItem item, int targetCol, int targetRow, out int closestCol, out int closestRow)
    {
        closestCol = targetCol;
        closestRow = targetRow;

        // Buscar en un radio alrededor de la posición objetivo
        int maxRadius = Math.Max(Layout.Columns, Layout.Rows);

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            // Buscar en todas las direcciones
            for (int dr = -radius; dr <= radius; dr++)
            {
                for (int dc = -radius; dc <= radius; dc++)
                {
                    // Solo en el borde del radio actual
                    if (Math.Abs(dr) != radius && Math.Abs(dc) != radius)
                        continue;

                    int testCol = targetCol + dc;
                    int testRow = targetRow + dr;

                    if (testCol >= 1 && testCol <= Layout.Columns - item.ColumnSpan + 1 &&
                        testRow >= 1 && testRow <= Layout.Rows - item.RowSpan + 1 &&
                        !HasCollision(item, testCol, testRow))
                    {
                        closestCol = testCol;
                        closestRow = testRow;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private async Task<bool> TryMoveCollidingItems(GridItem movingItem, int targetCol, int targetRow)
    {
        var collidingItems = new List<GridItem>();

        // Encontrar todos los items que colisionan
        foreach (var otherItem in Layout.Items)
        {
            if (otherItem.Id == movingItem.Id)
                continue;

            bool colOverlap = targetCol < otherItem.Column + otherItem.ColumnSpan &&
                             targetCol + movingItem.ColumnSpan > otherItem.Column;

            bool rowOverlap = targetRow < otherItem.Row + otherItem.RowSpan &&
                             targetRow + movingItem.RowSpan > otherItem.Row;

            if (colOverlap && rowOverlap)
            {
                collidingItems.Add(otherItem);
            }
        }

        // Ordenar por proximidad al borde de salida
        collidingItems = collidingItems
            .OrderBy(i => Math.Abs(i.Column - targetCol) + Math.Abs(i.Row - targetRow))
            .ToList();

        // Intentar mover cada item que colisiona
        foreach (var collidingItem in collidingItems)
        {
            bool moved = false;

            // Probar diferentes direcciones, priorizando la dirección opuesta al movimiento
            var directions = new List<(int dx, int dy, string direction)>
            {
                // Primero intentar mover en la dirección opuesta al movimiento
                (movingItem.ColumnSpan, 0, "derecha"),
                (0, movingItem.RowSpan, "abajo"),
                (-movingItem.ColumnSpan, 0, "izquierda"),
                (0, -movingItem.RowSpan, "arriba")
            };

            // Ordenar direcciones basadas en cuál tiene más espacio
            directions = directions
                .OrderByDescending(d => GetAvailableSpace(collidingItem, d.dx, d.dy))
                .ToList();

            foreach (var (dx, dy, _) in directions)
            {
                int newCol = collidingItem.Column + dx;
                int newRow = collidingItem.Row + dy;

                if (newCol >= 1 && newCol + collidingItem.ColumnSpan - 1 <= Layout.Columns &&
                    newRow >= 1 && newRow + collidingItem.RowSpan - 1 <= Layout.Rows &&
                    !HasCollision(collidingItem, newCol, newRow, false))
                {
                    // Mover temporalmente para verificar colisiones en cadena
                    var originalCol = collidingItem.Column;
                    var originalRow = collidingItem.Row;

                    collidingItem.Column = newCol;
                    collidingItem.Row = newRow;

                    // Verificar si este movimiento causa más colisiones
                    bool causesChainCollision = false;
                    foreach (var otherItem in Layout.Items)
                    {
                        if (otherItem.Id == collidingItem.Id || otherItem.Id == movingItem.Id)
                            continue;

                        if (HasCollision(collidingItem, collidingItem.Column, collidingItem.Row, false))
                        {
                            causesChainCollision = true;
                            break;
                        }
                    }

                    if (!causesChainCollision)
                    {
                        moved = true;
                        break;
                    }
                    else
                    {
                        // Revertir si causa colisión en cadena
                        collidingItem.Column = originalCol;
                        collidingItem.Row = originalRow;
                    }
                }
            }

            if (!moved)
                return false;
        }

        return true;
    }

    private int GetAvailableSpace(GridItem item, int dx, int dy)
    {
        int available = 0;
        int testCol = item.Column + dx;
        int testRow = item.Row + dy;

        while (testCol >= 1 && testCol + item.ColumnSpan - 1 <= Layout.Columns &&
               testRow >= 1 && testRow + item.RowSpan - 1 <= Layout.Rows &&
               !HasCollision(item, testCol, testRow, false))
        {
            available++;
            testCol += dx;
            testRow += dy;
        }

        return available;
    }

    // ============ MÉTODOS DE INTERFAZ ============

    private void ResizeItemWidth(GridItem item, int delta)
    {
        int newColSpan = item.ColumnSpan + delta;

        // Validar límites
        if (newColSpan < 1)
            newColSpan = 1;
        if (newColSpan > Layout.Columns)
            newColSpan = Layout.Columns;
        if (item.Column + newColSpan - 1 > Layout.Columns)
            return;

        // Verificar si el nuevo tamaño causa colisiones
        if (HasCollision(item, item.Column, item.Row))
        {
            return;
        }

        item.ColumnSpan = newColSpan;
        LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
    }

    private void ResizeItemHeight(GridItem item, int delta)
    {
        int newRowSpan = item.RowSpan + delta;

        // Validar límites
        if (newRowSpan < 1)
            newRowSpan = 1;
        if (newRowSpan > Layout.Rows)
            newRowSpan = Layout.Rows;
        if (item.Row + newRowSpan - 1 > Layout.Rows)
            return;

        // Verificar si el nuevo tamaño causa colisiones
        if (HasCollision(item, item.Column, item.Row))
        {
            return;
        }

        item.RowSpan = newRowSpan;
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

        // Encontrar primera posición disponible
        bool placed = false;
        for (int row = 1; row <= Layout.Rows && !placed; row++)
        {
            for (int col = 1; col <= Layout.Columns && !placed; col++)
            {
                // Verificar si cabe
                if (col + newItem.ColumnSpan - 1 <= Layout.Columns &&
                    row + newItem.RowSpan - 1 <= Layout.Rows)
                {
                    // Verificar colisiones
                    bool canPlace = true;
                    foreach (var existingItem in Layout.Items)
                    {
                        bool colOverlap = col < existingItem.Column + existingItem.ColumnSpan &&
                                        col + newItem.ColumnSpan > existingItem.Column;
                        bool rowOverlap = row < existingItem.Row + existingItem.RowSpan &&
                                        row + newItem.RowSpan > existingItem.Row;

                        if (colOverlap && rowOverlap)
                        {
                            canPlace = false;
                            break;
                        }
                    }

                    if (canPlace)
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

        if (!placed)
        {
            // Si no hay espacio, agregar con tamaño mínimo
            newItem.ColumnSpan = 1;
            newItem.RowSpan = 1;
            Layout.Items.Add(newItem);
            SelectedItem = newItem;
        }

        LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
    }

    private void RemoveItem(GridItem item)
    {
        Layout.Items.Remove(item);
        if (SelectedItem?.Id == item.Id)
        {
            SelectedItem = null;
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