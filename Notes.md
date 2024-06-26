### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK, THIS IS A POC!

#### POC to finish
navigation contenttypeproperty (Like umbNav package)
rating property with stars
material icon picker property (Use thr icon picker on contenttypes)
Add TinyMCE editor - Need to think where to store the API key globally? Add settings and make settings UI uniform
Settings on RTE (Check Umbraco) also ChatGPT add in and media picker add in
.. Or have ChatGPT property editor
Restrict admin by ip address (Do in parameters set on SectionLayout then send to logout, store these in appsettings?)
Move ContentValue to EF Core as string too big to save Lists (What about block list editor?)
Show Audit data in a dashboard? Or a tree for settings
Go through all TODO comments and try to fix all warnings
Need to delete all migrations, create one and see if it works with SQLite & also SQLServer (look into how to ship with different migrations based on Db)
Sort admin UI/UX
Build Start site and record video
Build personal site and launch
Update ReadMe and add docs
Find someone to help with the nuget packaging. Need to install site, but have Components/Core as seperate nuget package that people reference in other projects

### Future
Add languages (Check videos)
EF Core Query Caching? (https://www.nuget.org/packages/Z.EntityFramework.Plus.EFCore/) - Anything else? Does this auto clear? ChatGPT?
