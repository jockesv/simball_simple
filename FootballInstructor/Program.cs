
using FootballInstructor.Domain;
using FootballInstructor.Configuration;

namespace FootballInstructor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

            var builder = WebApplication.CreateBuilder(args);

            // Configure AI settings
            builder.Services.Configure<AISettings>(builder.Configuration.GetSection("AISettings"));

            // Add services to the container based on configuration
            var aiSettings = builder.Configuration.GetSection("AISettings").Get<AISettings>() ?? new AISettings();
            
            Console.WriteLine($"Starting with AI Type: {aiSettings.AIType}");
            
            switch (aiSettings.AIType.ToLower())
            {
                case "reinforcementlearning":
                    builder.Services.AddSingleton<IPlayerService, RLPlayerService>();
                    Console.WriteLine("Using Reinforcement Learning AI");
                    break;
                case "ppo":
                    builder.Services.AddSingleton<IPlayerService, PPOPlayerService>();
                    Console.WriteLine("Using PPO AI");
                    break;
                case "heuristic":
                default:
                    builder.Services.AddSingleton<IPlayerService, PlayerService>();
                    Console.WriteLine("Using Heuristic AI");
                    break;
            }

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: MyAllowSpecificOrigins,
                                  policy =>
                                  {
                                      policy.AllowAnyOrigin()
                                        .AllowAnyHeader()
                                        .AllowAnyMethod();
                                  });
            });

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseCors(MyAllowSpecificOrigins);

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
