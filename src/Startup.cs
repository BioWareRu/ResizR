using System;
using System.IO;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace BioEngine.ResizR
{
    [UsedImplicitly]
    public class Startup
    {
        private static readonly Regex PathRegex = new Regex(
            "(?<folderPath>.*)/(?<imageName>.*?).(?<width>[0-9]+)x(?<height>[0-9]+).(?<format>[a-zA-Z]+)");

        [UsedImplicitly]
        public void ConfigureServices(IServiceCollection services)
        {
        }

        [UsedImplicitly]
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILogger<Startup> logger,
            IConfiguration configuration)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Run(async context =>
            {
                var path = System.Net.WebUtility.UrlDecode(context.Request.Path);
                var match = PathRegex.Match(path);
                if (match.Success)
                {
                    var folderPath = match.Groups["folderPath"].Value;
                    var imageName = match.Groups["imageName"].Value;
                    var format = match.Groups["format"].Value;

                    if (!int.TryParse(match.Groups["width"].Value, out int width))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Bad width");
                        return;
                    }
                    if (!int.TryParse(match.Groups["height"].Value, out int height))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Bad height");
                        return;
                    }
                    var destPath = $"{configuration["ROOT_PATH"]}/{folderPath}/{imageName}.{width}x{height}.{format}";
                    if (File.Exists(destPath))
                    {
                        await context.Response.SendFileAsync(destPath);
                    }
                    else
                    {
                        var sourcePath = $"{configuration["ROOT_PATH"]}/{folderPath}/{imageName}.{format}";
                        if (File.Exists(sourcePath))
                        {
                            var resizeOptions = new ResizeOptions
                            {
                                Mode = ResizeMode.Max,
                                Size = new Size(width, height)
                            };
                            try
                            {
                                using (var image = Image.Load(sourcePath, out var mime))
                                {
                                    image.Mutate(x => x.Resize(resizeOptions));
                                    image.Save(destPath);
                                    context.Response.ContentType = mime.DefaultMimeType;
                                }
                                await context.Response.SendFileAsync(destPath);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex.Message);
                                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                                await context.Response.WriteAsync("Internal error");
                            }
                        }
                        else
                        {
                            context.Response.StatusCode = StatusCodes.Status404NotFound;
                            await context.Response.WriteAsync("File not found");
                        }
                    }
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Bad Url");
                }
            });
        }
    }
}