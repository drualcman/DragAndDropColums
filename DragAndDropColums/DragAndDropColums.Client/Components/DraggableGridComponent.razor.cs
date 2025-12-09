namespace DragAndDropColums.Client.Components;

public partial class DraggableGridComponent : IDisposable
{
    [Parameter] public GridLayout Layout { get; set; } = new();
    [Parameter] public EventCallback<GridLayout> LayoutChanged { get; set; }

    private GridItem? _selectedItem;
    private DragState _dragState = new();
    private GridCollisionService _collisionService;
    private GridPlacementService _placementService;
    private GridStyleService _styleService;
    private DragService _dragService;
    private HashSet<(int, int)> _occupiedCells = new();

    private void InitializeServices()
    {
        _collisionService = new GridCollisionService(Layout);
        _placementService = new GridPlacementService(Layout, _collisionService);
        _styleService = new GridStyleService(Layout);
        _dragService = new DragService(Layout);
    }


    protected override void OnParametersSet()
    {
        InitializeServices();
        if (Layout.Items.Any() && _selectedItem == null)
        {
            _selectedItem = Layout.Items.First();
        }
        UpdateOccupiedCells();
    }

    private string GetGridStyle() => _styleService.GetGridStyle();

    private string GetItemStyle(GridItem item)
    {
        bool isSelected = _selectedItem?.Id == item.Id;
        bool isDragging = _dragState.DraggingItem?.Id == item.Id;
        return _styleService.GetItemStyle(item, isSelected, isDragging);
    }

    private async Task StartMouseDrag(MouseEventArgs e, GridItem item)
    {
        _dragState.DraggingItem = item;
        _selectedItem = item;
        _dragState.IsDragging = true;
        _dragState.DragCursorClass = "dragging";
        _dragState.DragStartMouse = ((int)e.ClientX, (int)e.ClientY);
        _dragState.DragStartCell = (item.Column, item.Row);
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleMouseMove(MouseEventArgs e)
    {
        if (!_dragState.IsDragging || _dragState.DraggingItem == null)
            return;

        _dragState.FinalDropTarget = _dragService.CalculateDragTarget(e, _dragState, _dragState.DraggingItem);

        if (_dragState.FinalDropTarget.HasValue)
        {
            (int col, int row) = _dragState.FinalDropTarget.Value;
            _dragState.HoverTarget = (col, row);
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleMouseUp(MouseEventArgs e)
    {
        if (_dragState.IsDragging && _dragState.DraggingItem != null &&
            _dragState.FinalDropTarget.HasValue)
        {
            (int col, int row) = _dragState.FinalDropTarget.Value;
            await _placementService.PlaceItemAsync(_dragState.DraggingItem, col, row);
            await LayoutChanged.InvokeAsync(Layout);
        }

        await CleanUpDrag();
    }

    private async Task CleanUpDrag()
    {
        _dragState.Reset();
        await InvokeAsync(StateHasChanged);
    }

    private async Task MoveSelectedItem(int deltaCol, int deltaRow)
    {
        if (_selectedItem == null)
            return;

        int newCol = _selectedItem.Column + deltaCol;
        int newRow = _selectedItem.Row + deltaRow;

        await MoveItem(newCol, newRow);
    }

    private async Task MoveItem(int newCol, int newRow)
    {
        if (_selectedItem == null)
            return;

        await _placementService.PlaceItemAsync(_selectedItem, newCol, newRow);
        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }

    private async Task SelectItem(GridItem item)
    {
        if (_selectedItem?.Id == item.Id)
            return;
        _selectedItem = item;
        await InvokeAsync(StateHasChanged);
    }

    private async Task CellClicked(int col, int row)
    {
        if (_dragState.IsDragging)
            return;

        GridItem? itemAtCell = _collisionService.FindItemAtPosition(col, row);

        if (itemAtCell != null)
        {
            await SelectItem(itemAtCell);
            return;
        }
        await MoveItem(col, row);
    }

    private async Task DeselectItem()
    {
        _selectedItem = null;
        await InvokeAsync(StateHasChanged);
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
                if (_selectedItem != null)
                {
                    await RemoveItem(_selectedItem);
                }
                break;
            case "Escape":
                await DeselectItem();
                break;
            case " ":
                if (_selectedItem != null)
                {
                    await DeselectItem();
                }
                else if (Layout.Items.Any())
                {
                    _selectedItem = Layout.Items.First();
                    await InvokeAsync(StateHasChanged);
                }
                break;
        }
    }

    private async Task RemoveItem(GridItem item)
    {
        Layout.Items.Remove(item);
        if (_selectedItem?.Id == item.Id)
        {
            _selectedItem = Layout.Items.FirstOrDefault();
        }

        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }
    private void UpdateOccupiedCells()
    {
        _occupiedCells.Clear();
        foreach (var item in Layout.Items)
        {
            var cells = _collisionService.GetItemCells(item);
            _occupiedCells.UnionWith(cells);
        }
    }

    private async Task ResizeItemWidth(GridItem item, int delta)
    {
        int newColSpan = item.ColumnSpan + delta;

        if (newColSpan < 1)
        {
            newColSpan = 1;
        }

        if (newColSpan > Layout.Columns)
        {
            newColSpan = Layout.Columns;
        }

        if (item.Column + newColSpan - 1 > Layout.Columns)
        {
            return;
        }

        int originalColSpan = item.ColumnSpan;
        item.ColumnSpan = newColSpan;

        if (_collisionService.HasCollision(item, item.Column, item.Row))
        {
            item.ColumnSpan = originalColSpan;
            return;
        }

        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ResizeItemHeight(GridItem item, int delta)
    {
        int newRowSpan = item.RowSpan + delta;

        if (newRowSpan < 1)
        {
            newRowSpan = 1;
        }

        if (newRowSpan > Layout.Rows)
        {
            newRowSpan = Layout.Rows;
        }

        if (item.Row + newRowSpan - 1 > Layout.Rows)
        {
            return;
        }

        int originalRowSpan = item.RowSpan;
        item.RowSpan = newRowSpan;

        if (_collisionService.HasCollision(item, item.Column, item.Row))
        {
            item.RowSpan = originalRowSpan;
            return;
        }

        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }


    private async Task AddNewItem()
    {
        var newItem = GridItemFactory.CreateNewItem(Layout.Items.Count + 1);

        if (_placementService.TryPlaceNewItem(newItem))
        {
            Layout.Items.Add(newItem);
            _selectedItem = newItem;
            await LayoutChanged.InvokeAsync(Layout);
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ResetGrid()
    {
        Layout.Items.Clear();
        _selectedItem = null;
        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }

    private void SaveLayout()
    {
        string json = JsonSerializer.Serialize(Layout, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine("Layout guardado:");
        Console.WriteLine(json);
    }

    private async Task CloseEditor()
    {
        _selectedItem = null;
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        _collisionService = null;
        _placementService = null;
        _styleService = null;
        _dragService = null;

    }
}
