### Entity Framework

To update and make changes to the EF Core model, the migrations are in the .Core project. Migrations are auto applied at startup in the Program.cs.

```
dotnet tool update --global dotnet-ef

cd ZauberCMS.Core
dotnet ef --startup-project ../ZauberCMS.Web/ migrations add Initial -o "Data/Migrations"

// Optional - as migrations are run on startup  
dotnet ef database update --context ZauberDbContext
```

### Docs To Write

IContentView
IContentProperty  
IContentPropertySettings
IDataListSource
ICustomContentComponent

ISection
ISectionDashboard
ISectionNav

IContentBlock
IContentBlowPreview

IStorageProvider

Querying for data