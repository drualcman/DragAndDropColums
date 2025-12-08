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

    // Para trackear la posición inicial del drag
    private (int StartX, int StartY, int StartCol, int StartRow)? DragStartInfo { get; set; }

    protected override void OnInitialized()
    {
        // Asegurarse de que haya un item seleccionado al inicio si hay items
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

        string borderStyle = isSelected ? "3px solid #007bff" :
                            isDragging ? "3px dashed #ff6b6b" : "2px solid #333";

        return $"grid-column: {item.Column} / span {item.ColumnSpan}; " +
               $"grid-row: {item.Row} / span {item.RowSpan}; " +
               $"background-color: {item.BackgroundColor}; " +
               $"border: {borderStyle}; " +
               $"z-index: {(isSelected || isDragging ? "100" : "10")};" +
               $"{(isDragging ? "opacity: 0.8; cursor: grabbing;" : "cursor: grab;")}";
    }

    // ============ SIMULACIÓN DE DRAG AND DROP CON MOUSE ============

    private void StartMouseDrag(MouseEventArgs e, GridItem item)
    {
        DraggingItem = item;
        SelectedItem = item;
        IsDragging = true;
        DragCursorClass = "dragging";

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

        // Solo actualizar si hay movimiento significativo (más de 5px)
        if (Math.Abs(deltaX) > 5 || Math.Abs(deltaY) > 5)
        {
            int deltaCol = (int)Math.Round((double)deltaX / cellSizeWithGap);
            int deltaRow = (int)Math.Round((double)deltaY / cellSizeWithGap);

            // Nueva posición potencial
            int newCol = Math.Max(1, Math.Min(startCol + deltaCol, Layout.Columns - DraggingItem.ColumnSpan + 1));
            int newRow = Math.Max(1, Math.Min(startRow + deltaRow, Layout.Rows - DraggingItem.RowSpan + 1));

            // Solo actualizar si cambió
            if (newCol != DropTarget?.Col || newRow != DropTarget?.Row)
            {
                DropTarget = (newCol, newRow);

                // Iluminar la celda de drop target
                HighlightDropTarget(newCol, newRow);

                StateHasChanged();
            }
        }
    }

    private async Task HandleMouseUp(MouseEventArgs e)
    {
        if (IsDragging && DraggingItem != null && DropTarget.HasValue)
        {
            // Intentar colocar el elemento
            var (col, row) = DropTarget.Value;
            await TryPlaceItemWithPush(DraggingItem, col, row);
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

        // Limpiar highlight de celdas
        ClearDropTargetHighlights();

        StateHasChanged();
    }

    // ============ MÉTODOS DE SELECCIÓN Y MOVIMIENTO ============

    private void SelectItem(GridItem item)
    {
        // Si ya está seleccionado, deseleccionar al hacer click otra vez
        if (SelectedItem?.Id == item.Id)
        {
            DeselectItem();
        }
        else
        {
            SelectedItem = item;
        }
        StateHasChanged();
    }

    private void DeselectItem()
    {
        SelectedItem = null;
        StateHasChanged();
    }

    private void CellClicked(int col, int row)
    {
        // Si hay un elemento siendo arrastrado, no hacer nada con el click
        if (IsDragging)
            return;

        // Primero buscar si hay un elemento en esta celda
        var itemAtCell = FindItemAtCell(col, row);

        if (itemAtCell != null)
        {
            SelectItem(itemAtCell);
        }
        else if (SelectedItem != null)
        {
            // Mover el elemento seleccionado a esta celda
            _ = TryPlaceItemWithPush(SelectedItem, col, row);
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

        await TryPlaceItemWithPush(SelectedItem, newCol, newRow);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        // Verificar que el foco esté en el contenedor
        if (SelectedItem == null)
            return;

        // Prevenir comportamiento por defecto solo para las teclas que usamos
        if (e.Key == "ArrowUp" || e.Key == "ArrowDown" ||
            e.Key == "ArrowLeft" || e.Key == "ArrowRight" ||
            e.Key == "Delete" || e.Key == "Escape" || e.Key == " ")
        {
            //e.PreventDefault();
        }

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
                // Space para alternar selección
                if (SelectedItem != null)
                    DeselectItem();
                break;
        }
    }

    // ============ SISTEMA DE COLISIONES Y EMPUJE ============

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

    private List<GridItem> GetCollidingItems(GridItem item, int targetCol, int targetRow)
    {
        var collidingItems = new List<GridItem>();

        foreach (var otherItem in Layout.Items)
        {
            if (otherItem.Id == item.Id)
                continue;

            bool colOverlap = targetCol < otherItem.Column + otherItem.ColumnSpan &&
                             targetCol + item.ColumnSpan > otherItem.Column;

            bool rowOverlap = targetRow < otherItem.Row + otherItem.RowSpan &&
                             targetRow + item.RowSpan > otherItem.Row;

            if (colOverlap && rowOverlap)
            {
                collidingItems.Add(otherItem);
            }
        }

        return collidingItems;
    }

    private async Task TryPlaceItemWithPush(GridItem item, int targetCol, int targetRow)
    {
        // Ajustar para que quepa en la grid
        targetCol = Math.Max(1, Math.Min(targetCol, Layout.Columns - item.ColumnSpan + 1));
        targetRow = Math.Max(1, Math.Min(targetRow, Layout.Rows - item.RowSpan + 1));

        if (targetCol < 1 || targetRow < 1)
            return;

        // Si es la misma posición, no hacer nada
        if (item.Column == targetCol && item.Row == targetRow)
            return;

        // Calcular dirección del movimiento
        int deltaCol = targetCol - item.Column;
        int deltaRow = targetRow - item.Row;

        // Verificar si hay colisión
        if (HasCollision(item, targetCol, targetRow))
        {
            // Obtener items que colisionan
            var collidingItems = GetCollidingItems(item, targetCol, targetRow);

            if (collidingItems.Any())
            {
                // Intentar empujar los items en la dirección del movimiento
                if (await PushItemsInDirection(item, collidingItems, deltaCol, deltaRow))
                {
                    // Ahora colocar el item original
                    item.Column = targetCol;
                    item.Row = targetRow;
                    await LayoutChanged.InvokeAsync(Layout);
                    StateHasChanged();
                    return;
                }
                else
                {
                    // Si no se puede empujar, buscar posición alternativa
                    if (FindClosestPosition(item, targetCol, targetRow, out int closestCol, out int closestRow))
                    {
                        await TryPlaceItemWithPush(item, closestCol, closestRow);
                    }
                    return;
                }
            }
        }
        else
        {
            // No hay colisión, mover directamente
            item.Column = targetCol;
            item.Row = targetRow;
            await LayoutChanged.InvokeAsync(Layout);
            StateHasChanged();
        }
    }

    private async Task<bool> PushItemsInDirection(GridItem pushingItem, List<GridItem> itemsToPush, int deltaCol, int deltaRow)
    {
        // Determinar la dirección principal del movimiento
        bool movingRight = deltaCol > 0;
        bool movingLeft = deltaCol < 0;
        bool movingDown = deltaRow > 0;
        bool movingUp = deltaRow < 0;

        // Ordenar items a empujar según la dirección
        if (movingRight)
            itemsToPush = itemsToPush.OrderBy(i => i.Column).ToList();
        else if (movingLeft)
            itemsToPush = itemsToPush.OrderByDescending(i => i.Column).ToList();
        else if (movingDown)
            itemsToPush = itemsToPush.OrderBy(i => i.Row).ToList();
        else if (movingUp)
            itemsToPush = itemsToPush.OrderByDescending(i => i.Row).ToList();

        // Empujar cada item
        foreach (var item in itemsToPush)
        {
            // Calcular nueva posición basada en la dirección
            int newCol = item.Column;
            int newRow = item.Row;

            if (movingRight)
            {
                // Empujar a la derecha hasta donde empieza el item que empuja + su ancho
                newCol = pushingItem.Column + pushingItem.ColumnSpan;
            }
            else if (movingLeft)
            {
                // Empujar a la izquierda
                newCol = pushingItem.Column - item.ColumnSpan;
            }
            else if (movingDown)
            {
                // Empujar hacia abajo
                newRow = pushingItem.Row + pushingItem.RowSpan;
            }
            else if (movingUp)
            {
                // Empujar hacia arriba
                newRow = pushingItem.Row - item.RowSpan;
            }

            // Verificar que la nueva posición sea válida
            if (newCol < 1 || newCol + item.ColumnSpan - 1 > Layout.Columns ||
                newRow < 1 || newRow + item.RowSpan - 1 > Layout.Rows)
            {
                // Si no cabe, intentar mover en la dirección opuesta o hacia abajo
                if (movingRight || movingLeft)
                {
                    // Intentar mover hacia abajo
                    newRow = Math.Max(item.Row, pushingItem.Row + pushingItem.RowSpan);
                    newCol = item.Column;

                    // Verificar colisión en la nueva posición
                    if (HasCollision(item, newCol, newRow))
                    {
                        // Intentar encontrar posición alternativa
                        if (!await FindAndPushAlternativePosition(item, newCol, newRow))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        item.Column = newCol;
                        item.Row = newRow;
                    }
                }
                else if (movingDown || movingUp)
                {
                    // Intentar mover hacia la derecha
                    newCol = Math.Max(item.Column, pushingItem.Column + pushingItem.ColumnSpan);
                    newRow = item.Row;

                    // Verificar colisión en la nueva posición
                    if (HasCollision(item, newCol, newRow))
                    {
                        // Intentar encontrar posición alternativa
                        if (!await FindAndPushAlternativePosition(item, newCol, newRow))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        item.Column = newCol;
                        item.Row = newRow;
                    }
                }
            }
            else
            {
                // Verificar si la nueva posición tiene colisiones
                if (HasCollision(item, newCol, newRow))
                {
                    // Obtener items que colisionan en la nueva posición
                    var newCollisions = GetCollidingItems(item, newCol, newRow);

                    // Empujar recursivamente
                    if (!await PushItemsInDirection(item, newCollisions, deltaCol, deltaRow))
                    {
                        return false;
                    }
                }

                // Mover el item a la nueva posición
                item.Column = newCol;
                item.Row = newRow;
            }
        }

        return true;
    }

    private async Task<bool> FindAndPushAlternativePosition(GridItem item, int preferredCol, int preferredRow)
    {
        // Buscar posición alternativa cercana
        int searchRadius = Math.Max(Layout.Columns, Layout.Rows);

        for (int radius = 1; radius <= searchRadius; radius++)
        {
            // Probar posiciones en espiral alrededor de la preferida
            for (int dr = -radius; dr <= radius; dr++)
            {
                for (int dc = -radius; dc <= radius; dc++)
                {
                    // Solo probar en el perímetro del radio actual
                    if (Math.Abs(dr) != radius && Math.Abs(dc) != radius)
                        continue;

                    int testCol = preferredCol + dc;
                    int testRow = preferredRow + dr;

                    if (testCol >= 1 && testCol + item.ColumnSpan - 1 <= Layout.Columns &&
                        testRow >= 1 && testRow + item.RowSpan - 1 <= Layout.Rows)
                    {
                        if (!HasCollision(item, testCol, testRow))
                        {
                            item.Column = testCol;
                            item.Row = testRow;
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private bool FindClosestPosition(GridItem item, int targetCol, int targetRow, out int closestCol, out int closestRow)
    {
        closestCol = targetCol;
        closestRow = targetRow;

        int searchRadius = Math.Max(Layout.Columns, Layout.Rows);
        double closestDistance = double.MaxValue;

        for (int dr = -searchRadius; dr <= searchRadius; dr++)
        {
            for (int dc = -searchRadius; dc <= searchRadius; dc++)
            {
                int testCol = targetCol + dc;
                int testRow = targetRow + dr;

                if (testCol >= 1 && testCol + item.ColumnSpan - 1 <= Layout.Columns &&
                    testRow >= 1 && testRow + item.RowSpan - 1 <= Layout.Rows &&
                    !HasCollision(item, testCol, testRow))
                {
                    double distance = Math.Sqrt(dc * dc + dr * dr);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestCol = testCol;
                        closestRow = testRow;
                    }
                }
            }
        }

        return closestDistance < double.MaxValue;
    }

    // ============ VISUALIZACIÓN DE DROP TARGET ============

    private void HighlightDropTarget(int col, int row)
    {
        // En una implementación real, aquí podrías agregar clases CSS
        // o manipular el DOM para mostrar el highlight
        // Por ahora, lo manejamos en el CSS con una clase
    }

    private void ClearDropTargetHighlights()
    {
        // Limpiar cualquier highlight
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

        // Guardar tamaño original
        int originalColSpan = item.ColumnSpan;

        // Probar el nuevo tamaño
        item.ColumnSpan = newColSpan;

        // Verificar colisiones
        if (HasCollision(item, item.Column, item.Row))
        {
            // Revertir si hay colisión
            item.ColumnSpan = originalColSpan;
            return;
        }

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

        // Guardar tamaño original
        int originalRowSpan = item.RowSpan;

        // Probar el nuevo tamaño
        item.RowSpan = newRowSpan;

        // Verificar colisiones
        if (HasCollision(item, item.Column, item.Row))
        {
            // Revertir si hay colisión
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

        // Encontrar primera posición disponible
        bool placed = false;
        for (int row = 1; row <= Layout.Rows && !placed; row++)
        {
            for (int col = 1; col <= Layout.Columns && !placed; col++)
            {
                // Verificar si cabe
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
            // Intentar con tamaño mínimo
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

        // En una app real, podrías guardar en localStorage o enviar al servidor
        // Ejemplo: await localStorage.SetItemAsync("gridLayout", json);
    }

    private void CloseEditor()
    {
        SelectedItem = null;
        StateHasChanged();
    }
}