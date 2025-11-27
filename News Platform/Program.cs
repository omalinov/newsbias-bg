namespace News_Platform
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/error");
            }

            app.UseHttpsRedirection();

            app.MapGet("/ping", () => new { status = "ok" });

            app.Map("/error", (HttpContext context) =>
            {
                var exceptionHandlerFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                var ex = exceptionHandlerFeature?.Error;

                return Results.Problem(
                    title: "An unexpected error occurred.",
                    detail: ex?.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            });

            app.MapSourceEndpoints(connectionString);
            app.MapArticleEndpoints(connectionString);
            app.MapAppUserEndpoints(connectionString);
            app.MapUserPreferenceEndpoints(connectionString);
            app.MapBiasEndpoints(connectionString);
            app.MapUserFeedEndpoints(connectionString);

            app.Run();
        }
    }
};