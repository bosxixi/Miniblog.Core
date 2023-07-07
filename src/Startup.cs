namespace Miniblog.Core
{
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.StaticFiles;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Hosting;

    using Miniblog.Core.Services;

    using System;

    using WebEssentials.AspNetCore.OutputCaching;

    using WebMarkupMin.AspNetCore6;
    using WebMarkupMin.Core;

    using WilderMinds.MetaWeblog;

    using IWmmLogger = WebMarkupMin.Core.Loggers.ILogger;
    using MetaWeblogService = Miniblog.Core.Services.MetaWeblogService;
    using WmmNullLogger = WebMarkupMin.Core.Loggers.NullLogger;

    /// <summary>
    /// dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained true
    /// </summary>
    public class Startup
    {
        public Startup(IConfiguration configuration) => this.Configuration = configuration;

        public IConfiguration Configuration { get; }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(
                    webBuilder =>
                    {
                        global::System.Console.WriteLine(9170);
                        webBuilder
                            .UseStartup<Startup>()
                            .UseKestrel(c => { c.Listen(new System.Net.IPAddress(new byte[] { 0, 0, 0, 0 }), 9170); })
                            .ConfigureKestrel(options => options.AddServerHeader = false);
                    });

        public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

        /// <remarks>This method gets called by the runtime. Use this method to configure the HTTP request pipeline.</remarks>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            Console.WriteLine("v1.0");
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Shared/Error");
                app.UseHsts();
            }


            app.Use(
                (context, next) =>
                {
                    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                    return next();
                });



            {
                var fs = new FileServerOptions
                {
                    FileProvider = new PhysicalFileProvider(FileBlogService.FileBlogServicePathFull),
                    RequestPath = "/Posts",
                    EnableDirectoryBrowsing = true,
                };
                fs.StaticFileOptions.ServeUnknownFileTypes = true;

                var extensionProvider = new FileExtensionContentTypeProvider();
                extensionProvider.Mappings[".application"] = "application/x-ms-application";
                extensionProvider.Mappings[".manifest"] = "application/x-ms-manifest";
                extensionProvider.Mappings[".deploy"] = "application/octet-stream";
                extensionProvider.Mappings[".msp"] = "application/octet-stream";
                extensionProvider.Mappings[".msu"] = "application/octet-stream";
                extensionProvider.Mappings[".vsto"] = "application/x-ms-vsto";
                extensionProvider.Mappings[".xaml"] = "application/xaml+xml";
                extensionProvider.Mappings[".xbap"] = "application/x-ms-xbap";
                extensionProvider.Mappings[".exe"] = "application/vnd.microsoft.portable-executable";
                fs.StaticFileOptions.ContentTypeProvider = extensionProvider;
                app.UseFileServer(fs);
            }

            app.UseStatusCodePagesWithReExecute("/Shared/Error");
            app.UseWebOptimizer();

            app.UseStaticFilesWithCache();

            if (this.Configuration.GetValue<bool>("forcessl"))
            {
                app.UseHttpsRedirection();
            }

            app.UseMetaWeblog("/metaweblog");
            app.UseAuthentication();

            app.UseOutputCaching();
            app.UseWebMarkupMin();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(
                endpoints =>
                {
                    endpoints.MapControllerRoute("default", "{controller=Blog}/{action=Index}/{id?}");
                });
        }

        /// <remarks>This method gets called by the runtime. Use this method to add services to the container.</remarks>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();
            services.AddControllersWithViews();
            services.AddRazorPages();

            services.AddSingleton<IUserServices, BlogUserServices>();
            services.AddSingleton<IBlogService, FileBlogService>();
            services.Configure<BlogSettings>(this.Configuration.GetSection("blog"));
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddMetaWeblog<MetaWeblogService>();

            // Progressive Web Apps https://github.com/madskristensen/WebEssentials.AspNetCore.ServiceWorker
            services.AddProgressiveWebApp(
                new WebEssentials.AspNetCore.Pwa.PwaOptions
                {
                    OfflineRoute = "/shared/offline/"
                });

            // Output caching (https://github.com/madskristensen/WebEssentials.AspNetCore.OutputCaching)
            services.AddOutputCaching(
                options =>
                {
                    options.Profiles["default"] = new OutputCacheProfile
                    {
                        Duration = 3600
                    };
                });

            // Cookie authentication.
            services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(
                    options =>
                    {
                        options.LoginPath = "/login/";
                        options.LogoutPath = "/logout/";
                    });

            // HTML minification (https://github.com/Taritsyn/WebMarkupMin)
            services
                .AddWebMarkupMin(
                    options =>
                    {
                        options.AllowMinificationInDevelopmentEnvironment = true;
                        options.DisablePoweredByHttpHeaders = true;
                    })
                .AddHtmlMinification(
                    options =>
                    {
                        options.MinificationSettings.RemoveOptionalEndTags = false;
                        options.MinificationSettings.WhitespaceMinificationMode = WhitespaceMinificationMode.Safe;
                    });
            services.AddSingleton<IWmmLogger, WmmNullLogger>(); // Used by HTML minifier

            // Bundling, minification and Sass transpilation (https://github.com/ligershark/WebOptimizer)
            services.AddWebOptimizer(
                pipeline =>
                {
                    pipeline.MinifyJsFiles();
                    pipeline.CompileScssFiles()
                            .InlineImages(1);
                });
        }
    }
}
