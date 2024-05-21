### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK, THIS IS A POC!

#### POC to finish

autocomplete contenttypepproperty
numeric contenttypeproperty
icon picker contenttypeproperty
rating contenttypeproperty
select bar contenttypeproperty
navigation contenttypeproperty
Change trees to use own model with a type so can have folders
Icons on ContentTypes- Show in trees (Use Icon picker)
Make everything else pluggable (Sections, trees etc..)
Add in settings, each as their own tree not heading (Content Types, Languages (Add TODO), ViewComponents (Just list and allow view only))
Need to think about ContentTypes not used as pages (Think grid editor and list block)
Settings on RTE
Update ReadMe and add docs

#### This can be done after showing off POC
Refreshtree issues
Move ContentTypeProperty & ContentValue to EF Core as string too big to save Lists  

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