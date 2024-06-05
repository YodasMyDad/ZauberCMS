### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK, THIS IS A POC!

#### POC to finish
Make content tree into re-usable component, and use in multinode treepicker

Media Tree, Section etc... (And media picker) only show folders in the tree
Members (Including admins) and member picker
Paths (For breadcrumbs etc...) - Think about what happens if a page is moved
Think about save events of properties too, change data before being saved and loaded
make validated plugin when saving into a plugin (Where the Content and/or ContentType is passed in) and the result is returned
Sort AppState events, on things like deleted etc.. Is the naming correct make sure everything updates (Think about tree branches closing when creating child content?)
navigation contenttypeproperty (Copy umbNav)
Need a way from core to register things in startup
Need a default storage thing too for quick easy storage (Check BlogFodder too for other stuff!)
EF Core Query Caching? (https://www.nuget.org/packages/Z.EntityFramework.Plus.EFCore/) - Anything else? Does this auto clear? ChatGPT?
Update query on content and content type to simplyfy getting data (Have QueryContent and SingleContent for commands to keep it simple?)
rating property
material icon picker property (Use thr icon picker on contenttypes)
Settings on RTE (Check Umbraco) also ChatGPT add in and media picker add in
.. Or have ChatGPT property editor
Move ContentValue to EF Core as string too big to save Lists (What about block list editor?)
Need to delete all migrations, create one and see if it works with SQLite & also SQLServer (look into how to ship with different migrations based on Db)
Find someone to help with the nuget packaging. Need to install site, but have Components/Core as seperate nuget package that people reference in other projects
Build personal site and launch
Add languages (Check Blazor book)
Update ReadMe and add docs

