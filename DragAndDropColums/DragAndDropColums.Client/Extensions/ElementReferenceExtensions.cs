namespace DragAndDropColums.Client.Extensions;

public static class ElementReferenceExtensions
{
    extension(ElementReference element)
    {
        public async Task<BoundingClientRect> GetBoundingClientRectAsync(IJSRuntime js)
        {
            return await js.InvokeAsync<BoundingClientRect>("eval",
                $@"(function() {{
                var el = document.querySelector('[data-ref=""{element.Id}""]') || document.getElementById('{element.Id}');
                return el ? el.getBoundingClientRect() : null;
            }})()");
        }
    }
}
