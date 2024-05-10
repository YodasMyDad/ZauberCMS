### Entity Framework

To update and make changes to the EF Core model, the migrations are in the .Core project. Migrations are auto applied at startup in the Program.cs.

```
dotnet tool update --global dotnet-ef

cd ZauberCMS.Core
dotnet ef --startup-project ../ZauberCMS.Web/ migrations add Initial -o "Data/Migrations"

// Optional - as migrations are run on startup  
dotnet ef database update --context BlogFodderDbContext
```

### TODO

Add ability to create a content type, add properties to content types and save them
Add ability to create content from a content type, save to db (With a slug, sort order) and then list in the tree, when click item in tree, load saved content and update 
Make the Content Section & Settings Section dynamic so anyone can create sections and add pages