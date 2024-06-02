### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK, THIS IS A POC!

#### POC to finish
Block List Editor - Re-use contenteditor, and open in dialog, need to have parameters for Content

Paths (For breadcrumbs etc...) - Think about what happens if a page is moved
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
Need to delete all migrations, create one and see if it works with SQLite
Update ReadMe and add docs

