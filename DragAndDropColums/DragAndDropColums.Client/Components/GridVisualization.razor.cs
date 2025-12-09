namespace DragAndDropColums.Client.Components;

public partial class GridVisualization<TData> : IDisposable
{
    [Parameter] public GridLayout<TData> Layout { get; set; } = new();
    [Parameter] public RenderFragment<TData>? ChildContent { get; set; }
    [Parameter] public GridItem<TData>? SelectedItem { get; set; }
    [Parameter] public EventCallback<GridItem<TData>?> SelectedItemChanged { get; set; }
    [Parameter] public EventCallback<GridLayout<TData>> LayoutChanged { get; set; }
    [Parameter] public EventCallback<GridItem<TData>> OnItemRemoved { get; set; }
    [Parameter] public bool AllowKeyboardControls { get; set; } = true;

    private DragState<TData> _dragState = new();
    private GridCollisionService<TData> _collisionService;
    private GridPlacementService<TData> _placementService;
    private GridStyleService<TData> _styleService;
    private DragService<TData> _dragService;
    private HashSet<(int, int)> _occupiedCells = new();

    protected override void OnParametersSet()
    {
        InitializeServices();
        UpdateOccupiedCells();
    }

    private void InitializeServices()
    {
        _collisionService = new GridCollisionService<TData>(Layout);
        _placementService = new GridPlacementService<TData>(Layout, _collisionService);
        _styleService = new GridStyleService<TData>(Layout);
        _dragService = new DragService<TData>(Layout);
    }

    private string GetGridStyle() => _styleService.GetGridStyle();

    private string GetItemStyle(GridItem<TData> item)
    {
        bool isSelected = SelectedItem?.Id == item.Id;
        bool isDragging = _dragState.DraggingItem?.Id == item.Id;
        return _styleService.GetItemStyle(item, isSelected, isDragging);
    }

    private async Task StartMouseDrag(MouseEventArgs e, GridItem<TData> item)
    {
        _dragState.DraggingItem = item;
        await SelectItem(item);

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

    private async Task SelectItem(GridItem<TData> item)
    {
        if (SelectedItem?.Id == item.Id)
            return;

        await SelectedItemChanged.InvokeAsync(item);
    }

    private async Task CellClicked(int col, int row)
    {
        if (_dragState.IsDragging)
            return;

        GridItem<TData>? itemAtCell = _collisionService.FindItemAtPosition(col, row);

        if (itemAtCell != null)
        {
            await SelectItem(itemAtCell);
            return;
        }

        // Si no hay item, mover el seleccionado si existe
        if (SelectedItem != null)
        {
            await MoveSelectedItem(col, row);
        }
    }

    private async Task MoveSelectedItem(int targetCol, int targetRow)
    {
        if (SelectedItem == null)
            return;

        await _placementService.PlaceItemAsync(SelectedItem, targetCol, targetRow);
        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }

    private async Task MoveSelectedItemByDelta(int deltaCol, int deltaRow)
    {
        if (SelectedItem == null)
            return;

        int newCol = SelectedItem.Column + deltaCol;
        int newRow = SelectedItem.Row + deltaRow;

        await MoveSelectedItem(newCol, newRow);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!AllowKeyboardControls)
            return;

        switch (e.Key)
        {
            case "ArrowUp":
                await MoveSelectedItemByDelta(0, -1);
                break;
            case "ArrowDown":
                await MoveSelectedItemByDelta(0, 1);
                break;
            case "ArrowLeft":
                await MoveSelectedItemByDelta(-1, 0);
                break;
            case "ArrowRight":
                await MoveSelectedItemByDelta(1, 0);
                break;
            case "Delete":
            case "Backspace":
                if (SelectedItem != null)
                {
                    await OnItemRemoved.InvokeAsync(SelectedItem);
                }
                break;
            case "Escape":
                await SelectedItemChanged.InvokeAsync(null);
                break;
            case " ":
                if (SelectedItem != null)
                {
                    await SelectedItemChanged.InvokeAsync(null);
                }
                else if (Layout.Items.Any())
                {
                    await SelectedItemChanged.InvokeAsync(Layout.Items.First());
                }
                break;
        }
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

    // Métodos públicos para que el padre pueda controlar el grid
    public async Task MoveItem(int col, int row)
    {
        if (SelectedItem == null)
            return;
        await MoveSelectedItem(col, row);
    }

    public async Task MoveItemByDelta(int deltaCol, int deltaRow)
    {
        await MoveSelectedItemByDelta(deltaCol, deltaRow);
    }

    public async Task ResizeItemWidth(GridItem<TData> item, int delta)
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

    public async Task ResizeItemHeight(GridItem<TData> item, int delta)
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

    public async Task DeselectItem()
    {
        await SelectedItemChanged.InvokeAsync(null);
    }

    public void Dispose()
    {
        _collisionService = null;
        _placementService = null;
        _styleService = null;
        _dragService = null;
    }
}