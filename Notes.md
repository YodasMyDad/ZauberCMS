### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK, THIS IS A POC!

Add ContentViews to the Content or ContentType? Then use that to render front end views (Think about this)
Move ContentTypeProperty & ContentValue to EF Core as string too big to save Lists
Create datatypes so we can have settings (See below), link to ContentTypeProperty
Style the admin similar to Umbraco
Create a simple front end site with nav and style (Will need to create API for getting data etc...)

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
   
DataType
 - Id
 - Name
 - Description
 - Settings
 - PropertyComponent
 - SettingsComponent