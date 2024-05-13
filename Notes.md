### TODO

IMPORTANT!! DO NOT WORRY ABOUT UI/UX!!! JUST MAKE WORK

Add ability to create a content type, add properties to content types and save them (Also update)
Add ability to create content from a content type, save to db (With a slug, sort order) and then list in the tree, when click item in tree, load saved content and update 
Make the Content Section & Settings Section dynamic so anyone can create sections and add pages
Move ContentTypeProperty & ContentValue to EF Core as string too big to save Lists
Create datatypes so we can have settings (See below), link to ContentTypeProperty

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