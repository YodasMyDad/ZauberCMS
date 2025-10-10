# ZauberCMS - The Blazor CMS

### Note: This is v4 RC and still in .NET 9 for you to test, when released this will be updated to .NET 10.

ZauberCMS is a fully featured CMS built in .NET 10. The CMS is inspired by Umbraco but built entirely in Blazor.

**Front End (Your website):** Fast static server-side rendering (static SSR) with the ability to add Blazor components for interactivity (As recommended by MS) using @rendermode, or use your favourite JS framework (Vue, AlpineJS etc...)

**Admin (Manage Website):** Built entirely with InteractiveServer and using Radzen components for the main UI.

The goal is simple: a CMS that’s simple, super easy to extend, highly modular, and doesn’t require learning a verbose front-end framework or dealing with complex build tools. With Blazor, customization is quick, straightforward and very .NET friendly. 
 
✅ Built With Blazor  
✅ Visual Page Builder & Editor (Optional)  
✅ Manage Content, Media & Users  
✅ Custom Languages & Cultures  
✅ SEO Features Built In (Sitemaps, Redirects + More)  
✅ Protect Content & Media (Role Based)  
✅ Highly & Easily Customizable Using C#  
✅ Full Documentation  
✅ And loads more...

Website: [www.zaubercms.com](https://www.zaubercms.com/)

You can also find us on [Twitter / X](https://twitter.com/zaubercms) and [Facebook](https://www.facebook.com/profile.php?id=61573440519581)

## Getting Started

Fastest way to get started building your own website is using the .NET Template, firstly install the ZauberCMS template (You must use rc release to get v4)

```ps
# Ensure we have the latest ZauberCMS templates
dotnet new install ZauberCMS.Template::4.0.0-rc.5.4 --force

# Create your CMS
dotnet new zaubercms -n "YourSiteName"
```

You can also use Nuget and full instructions on how to use it are below

https://aptitude.gitbook.io/zaubercms/getting-started/quick-start

**DO NOT USE THE SOURCE CODE TO BUILD YOUR SITE. USE THE TEMPLATE OR NUGET PACKAGE!**

## Getting Started Video

This video shows building a very simple site from scratch, hopefully you'll see how easy it is! This is an older version of the CMS and some of the services have changed, but the concepts are the same.

[![Starter Site Build YouTube Video](https://files.gitbook.com/v0/b/gitbook-x-prod.appspot.com/o/spaces%2FVr2cbdfxDGZK1u2Fd59w%2Fuploads%2FPInFco2RCtXOrEN3hFVY%2Fgetting-started.png?alt=media&token=58bcda5e-0cf8-4789-b797-fb0f85a174b4)](https://www.youtube.com/watch?v=BvULaHbiIEU)

## Example Site

This repo comes with a starter site example so you can see some of the most common concepts, if you clone this repo, build and run the **ZauberCMS.Web** project you will see the starter kit (Go to /admin, register an account to see everything).

**Use the example site:** If you want to use this starter site as a starting point for your own website. Remove the project from the source, and remove the project reference in the csproj and uncomment the nuget package reference.

    <ItemGroup>
      <!--Remove this line 👇🏼 and uncomment the below line-->
      <ProjectReference Include="..\ZauberCMS\ZauberCMS.csproj" />
      <!--<PackageReference Include="ZauberCMS" Version="4.0.0-rc.5.4" />-->
    </ItemGroup>

_Again, DO NOT  use the entire source code to build your own site. You are supposed to use the Nuget package (or Template) like above._ 

## Documentation

For full documentation click the link below

https://aptitude.gitbook.io/zaubercms

## Progress & Issues

Next large release will be **v5.0**. You can see the progress and release features here

**v5.0 Board**  
https://github.com/users/YodasMyDad/projects/9

## Screenshots

![image](https://aptitude.gitbook.io/~gitbook/image?url=https%3A%2F%2F417697475-files.gitbook.io%2F%7E%2Ffiles%2Fv0%2Fb%2Fgitbook-x-prod.appspot.com%2Fo%2Fspaces%252FVr2cbdfxDGZK1u2Fd59w%252Fuploads%252FhxtqIGPZ1wMcA2t0uOwW%252Fcontent.png%3Falt%3Dmedia%26token%3De32e0e71-5141-4280-90bc-38416c3665e9&width=768&dpr=4&quality=100&sign=c416ef3a&sv=1)

![image](https://aptitude.gitbook.io/~gitbook/image?url=https%3A%2F%2F417697475-files.gitbook.io%2F%7E%2Ffiles%2Fv0%2Fb%2Fgitbook-x-prod.appspot.com%2Fo%2Fspaces%252FVr2cbdfxDGZK1u2Fd59w%252Fuploads%252FCdGRoHmlULvm88BVp6W6%252Fmedia.png%3Falt%3Dmedia%26token%3D1dc107d2-5932-45b0-9997-a2ca9259b5e0&width=768&dpr=4&quality=100&sign=3390e9ae&sv=1)

![image](https://aptitude.gitbook.io/~gitbook/image?url=https%3A%2F%2F417697475-files.gitbook.io%2F%7E%2Ffiles%2Fv0%2Fb%2Fgitbook-x-prod.appspot.com%2Fo%2Fspaces%252FVr2cbdfxDGZK1u2Fd59w%252Fuploads%252FhjwyfhOsRZ6nlfpiRa84%252Fusers.png%3Falt%3Dmedia%26token%3Deb6d29d2-3193-4787-afbf-2f36e999e48c&width=768&dpr=4&quality=100&sign=a68ff483&sv=1)

![image](https://aptitude.gitbook.io/~gitbook/image?url=https%3A%2F%2F417697475-files.gitbook.io%2F%7E%2Ffiles%2Fv0%2Fb%2Fgitbook-x-prod.appspot.com%2Fo%2Fspaces%252FVr2cbdfxDGZK1u2Fd59w%252Fuploads%252FhxWB77t9ZfQfEoc3lJze%252Fwebsite.png%3Falt%3Dmedia%26token%3D67111c34-7af5-471c-868d-b05b75c677ee&width=768&dpr=4&quality=100&sign=3a295916&sv=1)


