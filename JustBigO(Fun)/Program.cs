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

builder.Services.AddScoped<ICodeExecutor, DockerCodeExecutor>();

// --- MODIFICARE START: Configurare Identity cu Roluri și opțiuni de securitate ---
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    // Poți adăuga configurări suplimentare aici (parole, lockout etc.)
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
})
    .AddRoles<IdentityRole>() // Asigură suportul pentru RoleManager și [Authorize(Roles = "...")]
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Configurare explicită a politicilor (opțional, dar recomandat pentru claritate)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
});
// --- MODIFICARE FINAL ---

builder.Services.AddControllersWithViews();

// --- MODIFICARE AI AGENT START ---

builder.Services.AddSignalR();

var kernelBuilder = Kernel.CreateBuilder();

// Connect to your local Ollama instance instead of OpenAI
kernelBuilder.AddOpenAIChatCompletion(
    modelId: "llama3.2", // The model you downloaded
    apiKey: "NoKeyNeeded",          // Ollama ignores this, so we just pass a dummy string
    endpoint: new Uri("http://127.0.0.1:11434/v1") // Ollama's local default address
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

    // Aplică automat migrările la pornire
    await db.Database.MigrateAsync();

    // Rulează seeder-ele existente
    await ProblemSeeder.SeedAsync(db);
    await AdminSeeder.SeedAsync(roleManager, userManager);
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

// --- NOTĂ: Ordinea contează aici! Authentication trebuie să fie înaintea Authorization ---
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