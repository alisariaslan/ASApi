using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SarsMinimalApi.Context;
using SarsMinimalApi.Enums;
using SarsMinimalApi.Helpers;
using SarsMinimalApi.Models;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(o =>
{
#pragma warning disable CS8604 // Possible null reference argument.
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey
        (Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = false,
        ValidateIssuerSigningKey = true
    };
#pragma warning restore CS8604 // Possible null reference argument.
});

builder.Services.AddAuthorization();

builder.Services.AddDbContext<MyDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/apicheck", (HttpRequest request) => new ApiModel()
{
    Message = LangHelper.ConvertToResponse(request, 0, 0)
}).RequireAuthorization();

app.MapGet("/api/spamcheck", async (HttpRequest request, MyDbContext context) =>
{
    var isSpam = await IpHelper.SpamCheckAsync(request, context, "[GET]/api/spamcheck", 0,0);
    if (isSpam == SpamLevel.IpError)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 3) });
    else if (isSpam == SpamLevel.Spam)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 0) });
    else if (isSpam == SpamLevel.HardSpam)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 2) });
    else
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 1) });
}).RequireAuthorization();

#region EXPERIMENTAL
app.MapPost("/system/send", async (HttpRequest request, MyDbContext context, DynamicObjectModel cmd) =>
{
    if ((await IpHelper.SpamCheckAsync(request, context, "[POST]/system/send", 10, 3)) != SpamLevel.Ok)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 0) });
    if (!AuthHelper.IsFullyAuthorized(request, builder))
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 2, 0) });
    ProcessStartInfo psi = new ProcessStartInfo
    {
        FileName = "cmd.exe",
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    Process process = new Process();
    process.StartInfo = psi;
    process.Start();
    process.StandardInput.WriteLine(cmd.ToString());
    process.StandardInput.Flush();
    process.StandardInput.Close();
    string output = process.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    await DbHelper.SaveLog(context, "app.MapPost(/system/send)", $"{process.StartInfo.FileName} {process.StartInfo.Arguments}\noutput:{output}\nerror:{error}");
    return Results.Ok(new ApiModel() { Message = $"output:{output}\nerror:{error}"});
}).RequireAuthorization();
#endregion

app.MapPost("/mail/send", async (HttpRequest request, MyDbContext context, MailModel mailModel) =>
{
    if ((await IpHelper.SpamCheckAsync(request, context, "[POST]/mail/send", 600, 1)) != SpamLevel.Ok)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 0) });
    if (!AuthHelper.IsFullyAuthorized(request, builder))
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 2, 0) });
    await MailHelper.SendMailAsync(builder, mailModel);
    return Results.Ok(new ApiModel() { Message = LangHelper.ConvertToResponse(request, 3, 0) + mailModel.To });
}).RequireAuthorization();

app.MapDelete("/db/Tokens/{appid}", async (HttpRequest request, MyDbContext context, int appid) =>
{
    if ((await IpHelper.SpamCheckAsync(request, context, "[DEL]/db/Tokens/{appid}", 10, 1)) != SpamLevel.Ok)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 0) });
    if (!AuthHelper.IsFullyAuthorized(request, builder))
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 2, 0) });
    var datas = await context.Tokens
    .Where(e => e.AppId == appid) // Süresi dolmuş tokenleri seç
    .ToListAsync(); // Asenkron olarak liste olarak al
    context.Tokens.RemoveRange(datas); // Seçilen tokenleri kaldır
    await context.SaveChangesAsync();
    return Results.Ok(new ApiModel() { Message = LangHelper.ConvertToResponse(request, 4, 0) + (AppsFromIds)appid });
}).RequireAuthorization();

app.MapDelete("/db/Tokens", async (HttpRequest request, MyDbContext context) =>
{
    if ((await IpHelper.SpamCheckAsync(request, context, "[DEL]/db/Tokens", 10, 1)) != SpamLevel.Ok)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 0) });
    if (!AuthHelper.IsFullyAuthorized(request, builder))
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 2, 0) });
    context.Tokens.RemoveRange(context.Tokens);
    await context.SaveChangesAsync();
    return Results.Ok(new ApiModel() { Message = LangHelper.ConvertToResponse(request, 4, 1) });
}).RequireAuthorization();

app.MapPost("/auth/login/{appid}", [AllowAnonymous] async (HttpRequest request, MyDbContext context, LoginRegisterModel user, int appid) =>
{
    if ((await IpHelper.SpamCheckAsync(request, context, "[POST]/auth/login/{appid}", 10, 1)) != SpamLevel.Ok)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 0) });
    var userFromDB = await context.Users.FirstOrDefaultAsync(u => u.Username == user.Username);
    if (userFromDB is null)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 1, 0) });
    else if (userFromDB.Password != user.Password)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 1, 1) });
    #region Create token
    var issuer = builder.Configuration["Jwt:Issuer"];
    var audience = builder.Configuration["Jwt:Audience"];
#pragma warning disable CS8604 // Possible null reference argument.
    var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]);
#pragma warning restore CS8604 // Possible null reference argument.
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
        new Claim("Id", Guid.NewGuid().ToString()),
        new Claim(JwtRegisteredClaimNames.Sub, user.Username),
        new Claim(JwtRegisteredClaimNames.Jti,
            Guid.NewGuid().ToString())
    }),
        Expires = DateTime.UtcNow.AddDays(1),
        Issuer = issuer,
        Audience = audience,
        SigningCredentials = new SigningCredentials
        (new SymmetricSecurityKey(key),
        SecurityAlgorithms.HmacSha512Signature)
    };
    var tokenHandler = new JwtSecurityTokenHandler();
    var token = tokenHandler.CreateToken(tokenDescriptor);
    var jwtToken = tokenHandler.WriteToken(token);
    var stringToken = tokenHandler.WriteToken(token);
    #endregion
    #region Save token to DB
    TokenModel tokenModel = new TokenModel() { UserId = userFromDB.Id, Token = stringToken, AppId = appid };
    context.Tokens.Add(tokenModel);
    await context.SaveChangesAsync();
    #endregion
    #region Clear expired tokens
    var expiredTokens = await context.Tokens
    .Where(e => e.CreationDate >= DateTime.UtcNow.AddDays(1)) // Süresi dolmuş tokenleri seç
    .ToListAsync(); // Asenkron olarak liste olarak al
    context.Tokens.RemoveRange(expiredTokens); // Seçilen tokenleri kaldır
    await context.SaveChangesAsync();
    #endregion
    return Results.Ok(new ApiModel() { Data = stringToken, Message = LangHelper.ConvertToResponse(request, 1, 2) });
});

app.MapPost("/auth/register", [AllowAnonymous] async (HttpRequest request, MyDbContext context, LoginRegisterModel user) =>
{
    if ((await IpHelper.SpamCheckAsync(request, context, "[POST]/auth/register", 10, 1)) != SpamLevel.Ok)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 0) });
    #region Conflict and requirements check
    var userFromDB = await context.Users.FirstOrDefaultAsync(u => u.Username == user.Username);
    if (userFromDB is not null)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 5, 0) });
    if (user.Username.Length > 20 || user.Username.Length < 3)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 5, 1) });
    if (user.Password.Length > 30 || user.Password.Length < 5)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 5, 2) });
    #endregion
    #region Creating user
    UserModel model = new UserModel() { Username = user.Username, Password = user.Password, Email = user.Email };
    context.Users.Add(model);
    await context.SaveChangesAsync();
    #endregion
    #region Clear unverified users
    var expiredUsers = await context.Users
    .Where(e => e.RegisterDate <= DateTime.UtcNow.AddDays(7) && (e.IsEmailVerified == false && e.IsPhoneVerified == false)) // Süresi dolmuş tokenleri seç
    .ToListAsync(); // Asenkron olarak liste olarak al
    context.Users.RemoveRange(expiredUsers); // Seçilen tokenleri kaldır
    #endregion
    return Results.Ok(new ApiModel() { Message = LangHelper.ConvertToResponse(request, 1, 3) });
});

app.MapPost("/user/update/email", async (HttpRequest request, MyDbContext context, DynamicObjectModel uptadeObjectModel) =>
{
    if ((await IpHelper.SpamCheckAsync(request, context, "[POST]/user/update/email", 10, 1)) != SpamLevel.Ok)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 0) });
    var user = await DbHelper.GetUserFromToken(request, context);
    if (user is null)
        return Results.Json(new ApiModel() { Error = "Your token is invalid. Please login the your account again." });
    user.Email = uptadeObjectModel.Object.ToString();
    user.IsEmailVerified = false;
    context.Users.Update(user);
    await context.SaveChangesAsync();
    return Results.Ok(new ApiModel() { Message = "Your email is succesfully updated." });
}).RequireAuthorization();

app.MapPost("/user/verify/email/step1", async (HttpRequest request, MyDbContext context) =>
{
    if ((await IpHelper.SpamCheckAsync(request, context, "[POST]/user/verify/email/step1", 10, 1)) != SpamLevel.Ok)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 0) });
    var user = await DbHelper.GetUserFromToken(request, context);
    if (user is null)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 4, 2) });
    if (user.Email is null)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 6, 0) });
    #region Clear expired verifications
    var expires = await context.Verifications
    .Where(e => e.CreationDate >= DateTime.UtcNow.AddMinutes(1)) // Süresi dolmuş tokenleri seç
    .ToListAsync(); // Asenkron olarak liste olarak al
    context.Verifications.RemoveRange(expires); // Seçilen tokenleri kaldır
    await context.SaveChangesAsync();
    #endregion
    #region Clear old verifications
    var expires2 = await context.Verifications
    .Where(e => e.UserId == user.Id) // Süresi dolmuş tokenleri seç
    .ToListAsync(); // Asenkron olarak liste olarak al
    context.Verifications.RemoveRange(expires2); // Seçilen tokenleri kaldır
    await context.SaveChangesAsync();
    #endregion
    VerificationModel verificationModel = new VerificationModel() { UserId = user.Id };
    context.Verifications.Add(verificationModel);
    await context.SaveChangesAsync();
    string mailSubject = LangHelper.ConvertToResponse(request, 6, 1);
    string mailBody = LangHelper.ConvertToResponse(request, 6, 2) + "<h1>" + verificationModel.Code + "</h1>";
    await MailHelper.SendMailAsync(builder, new MailModel() { To = user.Email, Subject = mailSubject, Body = mailBody });
    return Results.Ok(new ApiModel() { Message = LangHelper.ConvertToResponse(request, 6, 3) + user.Email });
}).RequireAuthorization();

app.MapPost("/user/verify/email/step2", async (HttpRequest request, MyDbContext context, DynamicObjectModel dynamicObjectModel) =>
{
    if ((await IpHelper.SpamCheckAsync(request, context, "[POST]/user/verify/email/step2", 10, 1)) != SpamLevel.Ok)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 0) });
    var user = await DbHelper.GetUserFromToken(request, context);
    if (user is null)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 4, 2) });
    var verification = await context.Verifications.FirstOrDefaultAsync(a => a.Code == Convert.ToInt32(dynamicObjectModel.Object.ToString()));
    if (verification == null || (verification.UserId != user.Id))
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 6, 4) });
    user.IsEmailVerified = true;
    context.Users.Update(user);
    context.Verifications.Remove(verification);
    await context.SaveChangesAsync();
    return Results.Ok(new ApiModel() { Message = LangHelper.ConvertToResponse(request, 6, 5) });
}).RequireAuthorization();

app.MapPost("/user/update/password", async (HttpRequest request, MyDbContext context, DynamicObjectModel dynamicObjectModel) =>
{
    if ((await IpHelper.SpamCheckAsync(request, context, "[POST]/user/update/password", 10, 1)) != SpamLevel.Ok)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 7, 0) });
    var user = await DbHelper.GetUserFromToken(request, context);
    if (user is null)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 4, 2) });
    if (user.IsEmailVerified is null || user.IsEmailVerified is false)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 5, 3) });
    var newPassword = dynamicObjectModel.Object.ToString();
    if (newPassword == null || newPassword.Length > 30 || newPassword.Length < 5)
        return Results.Json(new ApiModel() { Error = LangHelper.ConvertToResponse(request, 5, 2) });
    user.Password = newPassword;
    context.Users.Update(user);
    await context.SaveChangesAsync();
    return Results.Ok(new ApiModel() { Message = LangHelper.ConvertToResponse(request, 5, 4) });
}).RequireAuthorization();

app.UseAuthentication();
app.UseAuthorization();
app.Run();
