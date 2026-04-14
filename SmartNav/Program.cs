using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Interfaces;
using SmartNav.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:4200") // dev
                                                      // .WithOrigins("https://your-angular-domain.com") // prod
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // only if using cookies / auth
        });
});

builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHttpClient<IAiSuggestionService, AiSuggestionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAngular");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await dbContext.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[FilteredPreference]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[FilteredPreference](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserID] INT NULL,
        [SelectedPreferenceCode] NVARCHAR(60) NULL,
        [SelectedPreferencePrompt] NVARCHAR(400) NULL,
        [VehicleSize] NVARCHAR(32) NULL,
        [AvoidTolls] BIT NOT NULL CONSTRAINT [DF_FilteredPreference_AvoidTolls] DEFAULT(0),
        [AvoidHighways] BIT NOT NULL CONSTRAINT [DF_FilteredPreference_AvoidHighways] DEFAULT(0),
        [AvoidFerries] BIT NOT NULL CONSTRAINT [DF_FilteredPreference_AvoidFerries] DEFAULT(0),
        [TrafficTimeMode] NVARCHAR(24) NULL,
        [TrafficStartDateTime] DATETIME2 NULL,
        [TrafficEndDateTime] DATETIME2 NULL,
        [IncludeEvChargingStations] BIT NOT NULL CONSTRAINT [DF_FilteredPreference_IncludeEvChargingStations] DEFAULT(0),
        [StationsJson] NVARCHAR(MAX) NULL,
        [AppliedAt] DATETIME2 NOT NULL CONSTRAINT [DF_FilteredPreference_AppliedAt] DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT [PK_FilteredPreference] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_FilteredPreference_User_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User]([Id]) ON DELETE SET NULL
    );

    CREATE INDEX [IX_FilteredPreference_UserID_AppliedAt]
        ON [dbo].[FilteredPreference]([UserID], [AppliedAt]);
END");
}

app.UseAuthorization();

app.MapControllers();

app.Run();
