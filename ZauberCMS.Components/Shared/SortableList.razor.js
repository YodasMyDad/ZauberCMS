export function init(id, group, pull, put, sort, handle, filter, component, forceFallback, ignoreDomChanges) {
    var sortable = new Sortable(document.getElementById(id), {
        animation: 200,
        group: {
            name: group,
            pull: pull || true,
            put: put
        },
        filter: filter || undefined,
        sort: sort,
        forceFallback: forceFallback,
        handle: handle || undefined,
        onUpdate: (event) => {
            // Revert the DOM to match the .NET state
            if(!ignoreDomChanges){
                event.item.remove();
                event.to.insertBefore(event.item, event.to.childNodes[event.oldIndex]);    
            }
            
            // Notify .NET to update its model and re-render
            component.invokeMethodAsync('OnUpdateJS', event.oldDraggableIndex, event.newDraggableIndex);
        }
    });
}
