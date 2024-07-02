
#### POC to finish

Have an API keys dictionary in appSettings (and editor to pick the keys)
Restrict admin by ip address (Do in parameters set on SectionLayout then send to logout, store these in appsettings?)
Need a copy content type menu (Append Copy of to name and alias)
ContentType alias needs to be unique, need to check before new one created
Show Audit data in a dashboard? Or a tree for settings (Settings tree headings on Umb, Structure, Templating, Advanced)
 -- Think Content Types should be a tree with different branches
Re-do media, it's awful (Need to re-do provider to save media not files)
Go through all TODO comments and try to fix all warnings
Need to delete all migrations, create one and see if it works with SQLite & also SQLServer
Update ReadMe and add docs (How do other sites do it?)

### Videos to do
Build Starter site and record video (How deal with media in repo so users can see)
Add TinyMCE editor - Need to think where to store the API key globally? Add settings and make settings UI uniform
Settings on RTE (Check Umbraco) also ChatGPT add in and media picker add in
.. Or have ChatGPT property editor

### Alpha Release

URls - use the path to create the url? so the url reflects where the item is, make this optional/configurable
EF Core Query Caching? (https://www.nuget.org/packages/Z.EntityFramework.Plus.EFCore/) Does this auto clear?
look into how to ship with different migrations based on Db and apply as and when
Find someone to help with the nuget packaging. Need to install site, but have Components/Core as separate nuget package that people reference in other projects
Go through TODO comments left
Build personal site and launch
