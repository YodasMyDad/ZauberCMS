# ZauberCMS - A Blazor CMS (Inspired By Umbraco)

### Important! This project is a proof of concept and still a work in progress

ZauberCMS is a CMS built on Blazor. The admin is built in Blazor InteractiveServer (Majority of the admin components are using Razden components) and the front end (Your site/content) 
can be built in whatever you want, static, server, WASM. The CMS is inspired by Umbraco, and follows many of the same concepts.

I wanted a CMS that was aimed towards .Net Developers who wanted to use Blazor and also a CMS that is easily extendable and customisable without having to learn a front end framework.

You can see what items are left to do before this becomes an alpha release here

_Core tech used is: .Net 8, Blazor, EF Core, Identity, Mediatr, Radzen Components_

The video below shows the starter site being built. It covers building a site from nothing, to complete finished site. If you have never used, Umbraco then I highly recommend watching it.

**Add Starter Site Build Video**

## Getting Started

You should just be able to clone, and then run the project and the starter site should show. It's running on Sqlite, and it does support MSSQL, but right now you will have to follow the link below on how to build the migration files for MSSQL

Get it running with MSSQL

Currently, the starter site comes as part of the CMS, but this will move to it's own Nuget component once the CMS moves from POC to Alpha. So you will install a black CMS and then install the starter site afterwards.

### Admin Section

Once you have the site running, if you go to **/account/register** and create an account, the first user added to the site is set as an 'admin' and you will be redirected to the admin so you can update and edit content 

## Documentation

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
GlobalData  
IValidate  
AppState  
IStorageProvider  
Querying for data  
Saving data  
AccountLayout  
404Component  
ApiKeys

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
