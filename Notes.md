### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK, THIS IS A POC!

#### POC to finish
Check all usermnager etc.. done with scope
Sort Edit User - Show tabs etc... and merge, think about sort order merging
Make manage and account it's own section, with Layout added programatically from appSettings? Or have a settings section and from there?
Make the single edit media much better and have an extended content editor which saves to extended content
(RenderMode) Try removing rendermode completely and then adding it just to the admin manually?
Paths (For breadcrumbs etc...) - Think about what happens if a page is moved
Think about save events of properties too, change data before being saved and loaded
make validated plugin when saving into a plugin (Where the Content and/or ContentType is passed in) and the result is returned
Sort AppState events, on things like deleted etc.. Is the naming correct make sure everything updates (Think about tree branches closing when creating child content?)
navigation contenttypeproperty (Like umbNav package)
Need a way from core to register things in startup
Need a default storage thing too for quick easy storage (Check BlogFodder too for other stuff!)
rating property with stars
material icon picker property (Use thr icon picker on contenttypes)
Add TinyMCE editor - Need to think where to store the API key globally? Add settings and make settings UI uniform
Settings on RTE (Check Umbraco) also ChatGPT add in and media picker add in
.. Or have ChatGPT property editor
Restrict admin by ip address
Move ContentValue to EF Core as string too big to save Lists (What about block list editor?)
Go through all TODO comments and try to fix all warnings
Need to delete all migrations, create one and see if it works with SQLite & also SQLServer (look into how to ship with different migrations based on Db)
Sort admin UI/UX
Find someone to help with the nuget packaging. Need to install site, but have Components/Core as seperate nuget package that people reference in other projects
Add languages (Check Blazor book)
Build personal site and launch
Update ReadMe and add docs

### Future
EF Core Query Caching? (https://www.nuget.org/packages/Z.EntityFramework.Plus.EFCore/) - Anything else? Does this auto clear? ChatGPT?
