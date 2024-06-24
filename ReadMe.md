# ZauberCMS - A Blazor CMS (Inspired By Umbraco)

**Important! This project is still a proof of concept and a WIP. It will move to Beta once .Net 9 is released as there are features in it I want to use.**

ZauberCMS is a CMS built on Blazor. The admin is built in Blazor InteractiveServer (Majority of the admin components are using Razden components) and the front end (Your site/content) 
can be built in whatever you want, static, server, WASM. The CMS is heavily inspired by Umbraco, and follows many of the same concepts.

_Core tech used is: .Net 8, Blazor, EF Core, Identity, Mediatr, Radzen Components_

The video below shows the starter site being built. It covers building a site from nothing, to complete finished site. Currently, the starter site 
comes as part of the CMS, but this will move to it's own Nuget component once the CMS moves from POC to Alpha.

TODO - ADD Start Site Video

### Getting Started

Choose your DB. Comes with SQL Express OOTB, but supports MS SQL.

### Built In Property Editors

sss
ss
s

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
Saving data

### Entity Framework

To update and make changes to the EF Core model, the migrations are in the .Core project. Migrations are auto applied at startup in the Program.cs.

Firstly make sure your EF tooling is up to date

```
dotnet tool update --global dotnet-ef
```

If you want to add migrations for DB changes in the core, you do the following

```
cd ZauberCMS.Core
dotnet ef --startup-project ../ZauberCMS.Web/ migrations add Initial -o "Data/Migrations"

// Optional - as migrations are run on startup  
dotnet ef database update --context ZauberDbContext
```
