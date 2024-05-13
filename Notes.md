### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK

Try and render some content on the front of the site (Look for first root page) and show content
Move ContentTypeProperty & ContentValue to EF Core as string too big to save Lists
Create datatypes so we can have settings (See below), link to ContentTypeProperty
Make the Content Section & Settings Section dynamic so anyone can create sections and add pages

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