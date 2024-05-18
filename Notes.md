### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK, THIS IS A POC!

#### Show off POC
Add contextmenu to the Tree for Create, Delete and sort (Sort for Content only)  
Make sure Datatypes list name and description to make them useful
Refactor creating content types, so remove the button that appears, make it slicker  
Add settings to datatypes, so you set the settings when you add the datatype to the contenttype (Settings string, SettingsView Component)  
Add in settings, each as their own tree not heading (Content Types, Languages (Add TODO), ViewComponents (Just list and allow view only))  

#### This can be done after showing off POC
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