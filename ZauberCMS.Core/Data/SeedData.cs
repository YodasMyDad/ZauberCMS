using MediatR;
namespace ZauberCMS.Core.Data;

public static class SeedDataExtensions
{
    public static async Task SeedData(this ZauberDbContext dbContext, IMediator mediator)
    {
        /*// Firstly see if there is settings, if not then new site
        var settings = dbContext.SiteSettings.FirstOrDefault();
        if (settings == null)
        {
            // Create settings
            dbContext.SiteSettings.Add(new SiteSettings());
            
            // Create admin user

            var registerUserCommand = new RegisterUserCommand
            {
                AutoLogin = false,
                Username = "Admin",
                Email = "admin@admin.com",
                Password = "P@$$word1234"
            };
            var userResult = await mediator.Send(registerUserCommand);

            // Create Initial Category
           var category = new Category {Name = "Example Category"};
           var categoryCommand = new CreateUpdateCategoryCommand
           {
               Category = category
           };
           var categoryResult = await mediator.Send(categoryCommand);

           // Create Post Image
            var image = new BlogFodderFile
            {
                Url = "img/monster-1.png",
                DateCreated = DateTime.UtcNow,
                ItemId = "misc",
                FileSize = 1505608,
                FileType = BlogFodderFileType.Image
            };
            dbContext.Files.Add(image);

            await dbContext.SaveChangesAsync();
            
            // Create Post
            var postImage = dbContext.Files.FirstOrDefault();
            var postCategory = dbContext.Categories.FirstOrDefault();
            var postUser = dbContext.Users.FirstOrDefault();

            var post = new Post
            {
                Url = "example-post",
                DateCreated = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow,
                Name = "Thanks for checking out BlogFodder! I need your help 😊",
                MetaDescription =
                    "Really do appreciate you testing this app, but I'm looking for help moving this app forward.",
                PageTitle = "Thanks for checking out BlogFodder! I need your help 😊",
                Excerpt =
                    "Really do appreciate you testing this app, but I'm looking for help moving this app forward.",
                User = postUser,
                FeaturedImage = postImage,
            };
            post.Categories.Add(postCategory);
            dbContext.Posts.Add(post);

            var postContentItem = new PostContentItem
            {
                Post = post,
                PluginAlias = "RichTextEditorPlugin",
                PluginData =
                    @"<p>BlogFodder is an idea I had to create a Blazor, plugin based blog engine. My goal is to make a blog engine that is really easy to extend and just works as expected.</p>
<p>I will update the ReadMe and docs more over the coming weeks. But for example, the content in the post is created with plugins and the entire admin section is created with plugins too. Posts have ContentItems, and each content item could be a different editor plugin. Right now, I'm writing in the TinyMCE Editor Plugin. There is also a Markdown Editor plugin but I would really like to have more.&nbsp;</p>
<p>Some ideas would be</p>
<ul>
                <li>
                <p>Image Carousel Editor (Standard Carousel that slides but with options)</p>
                </li>
                <li>
                <p>Image Gallery Editor (Which options for layout)</p>
                </li>
                <li>
                <p>YouTube Video Editor (Insert YouTube Videos)</p>
                </li>
                <li>
                <p>Image Generator Editor (Use DALL-E to create AI images from text prompt)</p>
                </li>
                <li>
                <p>Embed Tweet Editor (Allows you to paste a tweet URL and displays it on the front end)</p>
                </li>
                <li>
                <p>Code Editor (Code editor like ACE that allows you to edit the code and highlights it correctly on the front end)</p>
                </li>
                <li>
                <p>ChatGPT Editor (Uses TinyMCE or similar to allow you to create content)</p>
                </li>
                <li>
                <p>Amazon Affiliate Editor (Allows you to select products and create comparison tables with affiliate links)</p>
                </li>
                </ul>"
            };
            
            dbContext.PostContentItems.Add(postContentItem);

            var plugin = new Plugin
            {
                PluginAlias = "PostSideBarPlugin",
                PluginData = "",
                Enabled = true,
                PluginSettings = @"{""Enabled"":true,""AboutBoxEnabled"":true,""AboutBoxTitle"":""Hi, I'm Lee"",""AboutBoxImageId"":null,""AboutBoxImageUrl"":null,""AboutBoxText"":""I've been wanting to build this blog for sometime, and I finally decided to give it a go. I'll keep trying to improve it over the next few months."",""AboutBoxImageHeight"":160,""AboutBoxImageWidth"":400,""LatestPostsAmount"":3}"
            };
            dbContext.Plugins.Add(plugin);
            
            await dbContext.SaveChangesAsync();
        }*/
    }
}