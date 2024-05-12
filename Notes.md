### TODO

Add ability to create a content type, add properties to content types and save them
Add ability to create content from a content type, save to db (With a slug, sort order) and then list in the tree, when click item in tree, load saved content and update 
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
     - Component
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