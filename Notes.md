### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK, THIS IS A POC!

#### POC to finish
Set @attribute [StreamRendering] for font end pages
@rendermode InteractiveServer FALSE rendering (remove from Routes) for all admin pages

Change contenttype dialog to list only elements or contenttypes or both
Block List Editor - Re-use contenteditor, and open in dialog, need to have parameters for Content
Think about save events of properties too, change data before being saved and loaded
make validated plugin when saving into a plugin (Where the Content and/or ContentType is passed in) and the result is returned
Sort AppState events, on things like deleted etc.. Is the naming correct
Need a way from core to register things in startup
Need a default storage thing too for quick easy storage (Check BlogFodder too for other stuff)
EF Core Query Caching? (https://www.nuget.org/packages/Z.EntityFramework.Plus.EFCore/)
Update query on content and content type to simplyfy getting data (Have QueryContent and SingleContent for commands to keep it simple?)
autocomplete contenttypepproperty
select bar contenttypeproperty
rating contenttypeproperty
navigation contenttypeproperty
Settings on RTE
Media
Members (Including admins)
Make ContentTypeProperty list nvarchar(Max) or Text (SQL lite)
Move ContentValue to EF Core as string too big to save Lists
multi URL picker
multinode treepicker
media picker
Update ReadMe and add docs


Static server-side rendering (static SSR)
By default, components use static server-side rendering (static SSR). The component renders to the response stream and interactivity isn't enabled.

In the following example, there's no designation for the component's render mode, so the component inherits its render mode from its parent. 
Since no ancestor component specifies a render mode, the following component is statically rendered on the server. The button isn't interactive and doesn't 
call the UpdateMessage method when selected. The value of message doesn't change, and the component isn't rerendered in response to UI events.

Maybe need to change, especially in the head? 