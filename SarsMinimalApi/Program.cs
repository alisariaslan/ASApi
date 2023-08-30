using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SarsMinimalApi.Context;
using SarsMinimalApi.Helpers;
using SarsMinimalApi.Models;
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
});

builder.Services.AddAuthorization();

builder.Services.AddDbContext<MyDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.MapGet("/api/apicheck", () => new ApiModel() { Data = "Welcome to SARS APIs." }).RequireAuthorization();

app.MapPost("/mail/send", async (HttpRequest request, MyDbContext context, MailModel mailModel) =>
{
	if (!AuthHelper.IsFullyAuthorized(request, builder))
		return Results.Json(new ApiModel() { Data = "You have no permission to do this action!" });
	await MailHelper.SendMailAsync(builder, mailModel);
	return Results.Ok(new ApiModel());
}).RequireAuthorization();

app.MapDelete("/db/Tokens/{appid}", async (HttpRequest request, MyDbContext context, int appid) =>
{
	if (!AuthHelper.IsFullyAuthorized(request, builder))
		return Results.Json(new ApiModel() { Data = "You have no permission to do this action!" });
	var datas = await context.Tokens
	.Where(e => e.AppId == appid) // Süresi dolmuş tokenleri seç
	.ToListAsync(); // Asenkron olarak liste olarak al
	context.Tokens.RemoveRange(datas); // Seçilen tokenleri kaldır
	await context.SaveChangesAsync();
	return Results.Ok(new ApiModel());
}).RequireAuthorization();

app.MapDelete("/db/Tokens", async (HttpRequest request, MyDbContext context) =>
{
	if (!AuthHelper.IsFullyAuthorized(request, builder))
		return Results.Json(new ApiModel() { Data = "You have no permission to do this action!" });
	context.Tokens.RemoveRange(context.Tokens);
	await context.SaveChangesAsync();
	return Results.Ok(new ApiModel());
}).RequireAuthorization();

app.MapPost("/auth/login/{appid}", [AllowAnonymous] async (MyDbContext context, LoginRegisterModel user, int appid) =>
{
	var userFromDB = await context.Users.FirstOrDefaultAsync(u => u.Username == user.Username);
	if (userFromDB is null)
		return Results.Json(new ApiModel() { Error = "This user is not exists" });
	else if (userFromDB.Password != user.Password)
		return Results.Json(new ApiModel() { Error = "Your password is incorrect" });
	#region Create token
	var issuer = builder.Configuration["Jwt:Issuer"];
	var audience = builder.Configuration["Jwt:Audience"];
	var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]);
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
	return Results.Ok(new ApiModel() { Data = stringToken });
});

app.MapPost("/auth/register", [AllowAnonymous] async (MyDbContext context, LoginRegisterModel user) =>
{
	#region Conflict and requirements check
	var userFromDB = await context.Users.FirstOrDefaultAsync(u => u.Username == user.Username);
	if (userFromDB is not null)
		return Results.Conflict(new ApiModel() { Error = "This user already exits! Please prefer another username." });
	if (user.Username.Length > 10 || user.Password.Length > 10)
		return Results.Json(new ApiModel() { Error = "Your username/password length cannot be greater than 10" });
	if (user.Username.Length < 1 || user.Password.Length < 1)
		return Results.Json(new ApiModel() { Error = "Your username/password length cannot be lesser than 1" });
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
	return Results.Ok(new ApiModel());
});

app.MapPost("/user/update/email", async (HttpRequest request, MyDbContext context, DynamicObjectModel uptadeObjectModel) =>
{
	var user = await DbHelper.GetUserFromToken(request, context);
	if (user is null)
		return Results.Json(new ApiModel() { Error = "Your token is invalid. Please login the your account again." });
	user.Email = uptadeObjectModel.Object.ToString();
	context.Users.Update(user);
	await context.SaveChangesAsync();
	return Results.Ok(new ApiModel());
}).RequireAuthorization();

app.MapPost("/user/verify/email/step1", async (HttpRequest request, MyDbContext context) =>
{
	var user = await DbHelper.GetUserFromToken(request, context);
	if (user is null)
		return Results.Json(new ApiModel() { Error = "Your token is invalid. Please login the your account again." });
	if (user.Email is null)
		return Results.Json(new ApiModel() { Error = "Your email information is empty. Please fill your email information." });
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
	string mailSubject = "SARS Account Activation";
	string mailBody = "Your verification code is: <h1>" + verificationModel.Code + "</h1>";
	await MailHelper.SendMailAsync(builder, new MailModel() { To = user.Email, Subject = mailSubject, Body = mailBody });
	return Results.Ok(new ApiModel());
}).RequireAuthorization();

app.MapPost("/user/verify/email/step2", async (HttpRequest request, MyDbContext context, DynamicObjectModel dynamicObjectModel) =>
{
	var user = await DbHelper.GetUserFromToken(request, context);
	if (user is null)
		return Results.Json(new ApiModel() { Error = "Your token is invalid. Please login the your account again." });
	var verification = await context.Verifications.FirstOrDefaultAsync(a => a.Code == Convert.ToInt32(dynamicObjectModel.Object.ToString()));
	if (verification == null || (verification.UserId != user.Id))
		return Results.Json(new ApiModel() { Error = "Your verification code is invalid. Please request new one." });
	user.IsEmailVerified = true;
	context.Users.Update(user);
	context.Verifications.Remove(verification);
	await context.SaveChangesAsync();
	return Results.Ok(new ApiModel());
}).RequireAuthorization();

app.MapPost("/user/update/password", async (HttpRequest request, MyDbContext context, DynamicObjectModel dynamicObjectModel) =>
{
	var user = await DbHelper.GetUserFromToken(request, context);
	if (user is null)
		return Results.Json(new ApiModel() { Error = "Your token is invalid. Please login the your account again." });
	if(user.IsEmailVerified is null || user.IsEmailVerified is false)
		return Results.Json(new ApiModel() { Error = "Your account is unverified. You cannot change your password. Please verify your account." });
	var newPassword = dynamicObjectModel.Object.ToString();
	if(newPassword == null || newPassword.Length > 30 || newPassword.Length < 1)
		return Results.Json(new ApiModel() { Error = "Your password is too short or too long." });
	user.Password = newPassword;
	context.Users.Update(user);
	await context.SaveChangesAsync();
	return Results.Ok(new ApiModel());
}).RequireAuthorization();

app.UseAuthentication();
app.UseAuthorization();
app.Run();
