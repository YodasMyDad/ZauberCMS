### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK, THIS IS A POC!

#### POC to finish

Make everything else pluggable (Sections, trees, Dashboards etc..) think about save events of properties too, change data before being saved and loaded
make validated plugin when saving into a plugin (Where the Content and/or ContentType is passed in) and the result is returned
Need a way from core to register things in startup
Need a default storage thing too for quick easy storage
Check BlogFodder too for other stuff

EF Core Query Caching? (https://www.nuget.org/packages/Z.EntityFramework.Plus.EFCore/)
Update query on content and content type to simplyfy getting data (Have QueryContent and SingleContent for commands to keep it simple?)
Add in settings, each as their own tree not heading (Content Types, Languages (Add TODO), ViewComponents (Just list and allow view only))
Need to think about ContentTypes not used as pages (Think grid editor and list block)
autocomplete contenttypepproperty
select bar contenttypeproperty
rating contenttypeproperty
navigation contenttypeproperty
Settings on RTE

Update ReadMe and add docs

#### This can be done after showing off POC
Media
Members (Including admins)
Refreshtree issues
Make ContentTypeProperty list nvarchar(Max) or Text (SQL lite)
Move ContentValue to EF Core as string too big to save Lists
Block List Editor
multi URL picker
multinode treepicker
media picker

### Structure

ContentType
 - Id
 - Name
 - Alias
   - List<ContentTypeProperty> - 
     - Id
     - Name
     - Alias (Generated from name)
     - Description
     - DataTypeId
     - DataType (EF)
     - Sort Order
     - TabAlias

Content
 - Id
 - Name
 - Url
 - ContentTypeId
 - SortOrder
 - ParentId
   - List<ContentValue>
     - ContentTypePropertyId
     - Value
     - Settings
   

Maybe.... 

DataType
 - Id
 - Name
 - Description
 - Settings
 - PropertyComponent
 - SettingsComponent