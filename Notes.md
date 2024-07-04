
#### POC Release TODO

Build Starter site
Update ReadMe and add docs (How do other sites do it?)
RE-Build Starter site and record video (How deal with test media in repo so users can see it?)

### Alpha Release TODO

Media Picker - Have context menu to Create Folder and Upload Files and open in Dialog
URls - use the path to create the url? so the url reflects where the item is, make this optional/configurable
EF Core Query Caching? (https://www.nuget.org/packages/Z.EntityFramework.Plus.EFCore/) Does this auto clear? Or do own with memory caching, have cached bool on command
look into how to ship with different migrations based on Db and apply as and when
SortableList (Sortable.js) issue with block list editor item contents disappearing when moved and also why there is lag (Move too quick, UI resets but the sort has worked)
ISeedData - Make an ISeedData that allows users to add seed data at start
Add TinyMCE editor - Add settings and make settings UI uniform, Settings on RTE (Check Umbraco) also ChatGPT add in and media picker add in (Or have ChatGPT property editor)
Find someone to help with the nuget packaging. Need to install site, but have Components/Core as separate nuget package that people reference in other projects
Build personal site and launch (Test on live server)
