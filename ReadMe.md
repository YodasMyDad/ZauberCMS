# ZauberCMS - The Blazor CMS

ZauberCMS is a fully featured CMS built in **.NET 9 and Blazor InteractiveServer**. The CMS is [inspired by Umbraco](https://umbraco.com/), and follows a number of the same concepts.

I wanted a CMS that was aimed towards .Net Developers who wanted to use Blazor and also a CMS that is easily extendable and customisable without 
having to learn a front end framework. Anyone that has Blazor experience and is interested in getting involved, [please contact me on X](https://twitter.com/YodasMyDad)

_Core tech used is: .Net 9, Blazor, EF Core, Identity, Mediatr, [Radzen Components](https://www.radzen.com/blazor-components/)_

## Getting Started

This repo comes with a starter site example, if you clone this repo, build and run the **ZauberCMS.Web** project you will see the starter kit (Go to /admin, register an account to see everything).

However, fastest way to get started building your own website is using the .Net Template, firstly install the ZauberCMS template (--force just makes sure you install the latest one)

`dotnet new install ZauberCMS.Template --force`

Then create your new solution and project (Project names should start with ZauberCMS so the plugin system picks them up) using the code below:

`dotnet new sln --name "YourSolutionName"`  
`dotnet new zaubercms -n "ZauberCMS.YourProjectName"`  
`dotnet sln add "ZauberCMS.YourProjectName"`

You can also use Nuget and full instructions on how to use it are below (I prefer this way myself)

https://aptitude.gitbook.io/zaubercms/getting-started/quick-start

## Starter Site Build

The video below shows the starter site being built. It covers building a site from nothing, to complete finished site. If you have never used Umbraco 
then I highly recommend watching it as it covers a lot of concepts.

[![Starter Site Build YouTube Video](https://aptitude.gitbook.io/~gitbook/image?url=https%3A%2F%2F417697475-files.gitbook.io%2F%7E%2Ffiles%2Fv0%2Fb%2Fgitbook-x-prod.appspot.com%2Fo%2Fspaces%252FVr2cbdfxDGZK1u2Fd59w%252Fuploads%252FQslhSO0S2IBKP3k7v0Qx%252Fyoutube-cover-2.png%3Falt%3Dmedia%26token%3Db4b0d4d1-35e7-4f72-a6a0-84a57ff49095&width=768&dpr=4&quality=100&sign=4611e2ee&sv=1)](https://www.youtube.com/watch?v=BvULaHbiIEU)

## Documentation

For full documentation click the link below

https://aptitude.gitbook.io/zaubercms

## Progress & Issues

After v2.0 the next big release will be **v2.5**. You can see the progress and release features here

https://github.com/users/YodasMyDad/projects/8

And issues found here

https://github.com/YodasMyDad/ZauberCMS/issues

## Screenshots

![image](https://aptitude.gitbook.io/~gitbook/image?url=https%3A%2F%2F417697475-files.gitbook.io%2F%7E%2Ffiles%2Fv0%2Fb%2Fgitbook-x-prod.appspot.com%2Fo%2Fspaces%252FVr2cbdfxDGZK1u2Fd59w%252Fuploads%252FhxtqIGPZ1wMcA2t0uOwW%252Fcontent.png%3Falt%3Dmedia%26token%3De32e0e71-5141-4280-90bc-38416c3665e9&width=768&dpr=4&quality=100&sign=c416ef3a&sv=1)

![image](https://aptitude.gitbook.io/~gitbook/image?url=https%3A%2F%2F417697475-files.gitbook.io%2F%7E%2Ffiles%2Fv0%2Fb%2Fgitbook-x-prod.appspot.com%2Fo%2Fspaces%252FVr2cbdfxDGZK1u2Fd59w%252Fuploads%252FCdGRoHmlULvm88BVp6W6%252Fmedia.png%3Falt%3Dmedia%26token%3D1dc107d2-5932-45b0-9997-a2ca9259b5e0&width=768&dpr=4&quality=100&sign=3390e9ae&sv=1)

![image](https://aptitude.gitbook.io/~gitbook/image?url=https%3A%2F%2F417697475-files.gitbook.io%2F%7E%2Ffiles%2Fv0%2Fb%2Fgitbook-x-prod.appspot.com%2Fo%2Fspaces%252FVr2cbdfxDGZK1u2Fd59w%252Fuploads%252FhjwyfhOsRZ6nlfpiRa84%252Fusers.png%3Falt%3Dmedia%26token%3Deb6d29d2-3193-4787-afbf-2f36e999e48c&width=768&dpr=4&quality=100&sign=a68ff483&sv=1)

![image](https://aptitude.gitbook.io/~gitbook/image?url=https%3A%2F%2F417697475-files.gitbook.io%2F%7E%2Ffiles%2Fv0%2Fb%2Fgitbook-x-prod.appspot.com%2Fo%2Fspaces%252FVr2cbdfxDGZK1u2Fd59w%252Fuploads%252FhxWB77t9ZfQfEoc3lJze%252Fwebsite.png%3Falt%3Dmedia%26token%3D67111c34-7af5-471c-868d-b05b75c677ee&width=768&dpr=4&quality=100&sign=3a295916&sv=1)


