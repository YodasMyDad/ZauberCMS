### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK, THIS IS A POC!

Create a simple front end site with nav and style (Will need to create API for getting data etc...)
Move ContentTypeProperty & ContentValue to EF Core as string too big to save Lists
Create datatypes so we can have settings (See below), link to ContentTypeProperty
Style the admin similar to Umbraco

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