### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK, THIS IS A POC!

#### POC to finish

Re-style media, it's awful
rating property with stars
Have an API keys dictionary in appSettings (and editor to pick the keys)
Add TinyMCE editor - Need to think where to store the API key globally? Add settings and make settings UI uniform
Settings on RTE (Check Umbraco) also ChatGPT add in and media picker add in
.. Or have ChatGPT property editor
Restrict admin by ip address (Do in parameters set on SectionLayout then send to logout, store these in appsettings?)
Need a copy content type menu
Show Audit data in a dashboard? Or a tree for settings (Settings tree headings on Umb, Structure, Templating, Advanced)
 -- Think Content Types should be a tree with different branches
Go through all TODO comments and try to fix all warnings
Need to delete all migrations, create one and see if it works with SQLite & also SQLServer
Build Start site and record video
Update ReadMe and add docs

### Alpha Release

Rework the media section, editing/UI it's awful
URls - use the path to create the url? so the url reflects where the item is, make this optional/configurable
EF Core Query Caching? (https://www.nuget.org/packages/Z.EntityFramework.Plus.EFCore/) Does this auto clear?
look into how to ship with different migrations based on Db and apply as and when
Find someone to help with the nuget packaging. Need to install site, but have Components/Core as separate nuget package that people reference in other projects
Go through TODO comments left
Build personal site and launch
