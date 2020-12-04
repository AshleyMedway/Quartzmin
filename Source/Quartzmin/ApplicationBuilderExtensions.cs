#if NETSTANDARD || NETCORE

using System;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

#if NETCORE
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
#endif

namespace Quartzmin
{
    public static class ApplicationBuilderExtensions
    {
        public static void UseQuartzmin(this IApplicationBuilder app, QuartzminOptions options, Action<Services> configure = null)
        {
            options = options ?? throw new ArgumentNullException(nameof(options));

            app.UseFileServer(options);

            var services = Services.Create(options);
            configure?.Invoke(services);

            app.Use(async (context, next) =>
            {
                context.Items[typeof(Services)] = services;
                await next.Invoke();
            });

            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    var ex = context.Features.Get<IExceptionHandlerFeature>().Error;
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(services.ViewEngine.ErrorPage(ex));
                });
            });

#if NETCORE
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(nameof(Quartzmin), "{controller=Scheduler}/{action=Index}");
            });
#endif

#if NETSTANDARD
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: nameof(Quartzmin),
                    template: "{controller=Scheduler}/{action=Index}");
            });
#endif
        }

        private static void UseFileServer(this IApplicationBuilder app, QuartzminOptions options)
        {
            IFileProvider fs;
            if (string.IsNullOrEmpty(options.ContentRootDirectory))
                fs = new ManifestEmbeddedFileProvider(Assembly.GetExecutingAssembly(), "Content");
            else
                fs = new PhysicalFileProvider(options.ContentRootDirectory);

            var fsOptions = new FileServerOptions()
            {
                RequestPath = new PathString("/Content"),
                EnableDefaultFiles = false,
                EnableDirectoryBrowsing = false,
                FileProvider = fs
            };

            app.UseFileServer(fsOptions);
        }

        public static void AddQuartzmin(this IServiceCollection services)
        {
            services.AddMvcCore()
                .AddApplicationPart(Assembly.GetExecutingAssembly())

#if NETSTANDARD
                .AddJsonFormatters();
#endif
#if NETCORE
                .AddNewtonsoftJson(o =>
                {
                    o.SerializerSettings.Converters.Add(new StringEnumConverter());
                    o.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                });
#endif

        }

    }
}

#endif
