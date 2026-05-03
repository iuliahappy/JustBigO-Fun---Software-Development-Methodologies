using JustBigO_Fun_.Data;
using JustBigO_Fun_.Services;
using JustBigO_Fun_.Services.AI;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using JustBigO_Fun_.Hubs;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// --- INREGISTRARE SERVICII ---
builder.Services.AddScoped<ICodeExecutor, DockerCodeExecutor>();
// Am folosit AddHttpClient pentru ca Agentul AI face request-uri externe pe internet
builder.Services.AddHttpClient<IComplexityAnalyzer, AgentComplexityAnalyzer>();
// -----------------------------

// --- CONFIGURARE IDENTITY ---
builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = true;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthorization(options => {
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
});
// ----------------------------

builder.Services.AddControllersWithViews();

// --- MODIFICARE AI AGENT START ---

builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// NEW: Create a custom, patient HttpClient that waits up to 10 minutes
var patientHttpClient = new HttpClient
{
    Timeout = TimeSpan.FromMinutes(10)
};

var kernelBuilder = Kernel.CreateBuilder();

// Connect to your local Ollama instance instead of OpenAI
kernelBuilder.AddOpenAIChatCompletion(
    modelId: "llama3.2",
    apiKey: "NoKeyNeeded",
    endpoint: new Uri("http://127.0.0.1:11434/v1"),
    httpClient: patientHttpClient // NEW: Tell Semantic Kernel to use our patient client!
);

builder.Services.AddSingleton(kernelBuilder.Build());

builder.Services.AddTransient<ICodeTranslatorAgent, SemanticKernelTranslator>();
// --- MODIFICARE AI AGENT FINAL ---


var app = builder.Build();

// Apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    await db.Database.MigrateAsync();

    // Decomenteaza aceste linii daca ai nevoie sa populezi din nou baza de date
    // await ProblemSeeder.SeedAsync(db);
    // await AdminSeeder.SeedAsync(roleManager, userManager);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapHub<TranslationHub>("/translationHub");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();