using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

class Program
{
	public static WebApplicationBuilder Builder;

	static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		Builder = builder;

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
				ValidIssuer = builder.Configuration["Jwt:TokenSignature"],
				ValidAudience = builder.Configuration["Jwt:TokenSignature"],
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:EncryptionKey"])),
				ValidateIssuer = true,
				ValidateAudience = true,
				ValidateLifetime = true,
				ValidateIssuerSigningKey = true
			};
#pragma warning restore CS8604 // Possible null reference argument.
		});

		builder.Services.AddAuthorization();

		builder.Services.AddDbContext<MyDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("MSSQL")));

		var app = builder.Build();

		Console.WriteLine($"FireWall:{builder.Configuration["FireWall"]}");
		Console.WriteLine($"SpamChecks:{builder.Configuration["SpamChecks"]}");
		Console.WriteLine($"IPLogs:{builder.Configuration["IPLogs"]}");
		Console.WriteLine($"Cloudflare:{builder.Configuration["Cloudflare"]}");
		Console.WriteLine($"FullAuthProtection:{builder.Configuration["FullAuthProtection"]}");
		Console.WriteLine($"EncryptionKey:{builder.Configuration["Jwt:EncryptionKey"]}");
		Console.WriteLine($"AdminToken:{builder.Configuration["Jwt:AdminToken"]}");
		Console.WriteLine($"TokenExpireAsHour:{builder.Configuration["Jwt:TokenExpireAsHour"]}");
		Console.WriteLine($"TokenSignature:{builder.Configuration["Jwt:TokenSignature"]}");
		Console.WriteLine($"MSSQL:{builder.Configuration["ConnectionStrings:MSSQL"]}");

		if (app.Environment.IsDevelopment())
		{
			app.UseSwagger();
			app.UseSwaggerUI();
		}

		app.MapGet("/", async (HttpRequest request, MyDbContext myDbContext) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[GET]/api/apicheck", 10, 10)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				return Results.Json(new ApiModel() { Message = await LangHelper.ConvertToResponse(myDbContext, request, 0, 0) });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x1", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		});

		app.MapGet("/spamcheck", async (HttpRequest request, MyDbContext myDbContext) =>
		{
			try
			{
				var spamLevel = await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[GET]/api/spamcheck", 0, 0);
				if (spamLevel == SpamLevel.IpError)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 3) });
				else if (spamLevel == SpamLevel.Spam)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				else if (spamLevel == SpamLevel.HardSpam)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 2) });
				else
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 1) });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x2", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		});

		app.MapPost("/auth/login/{appid}", [AllowAnonymous] async (HttpRequest request, MyDbContext myDbContext, LoginRegisterModel user, int appid) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[POST]/auth/login/{appid}", 10, 10)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				var userFromDB = await myDbContext.Users.FirstOrDefaultAsync(u => u.Username == user.Username);
				if (userFromDB is null)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 1, 0) });
				else if (userFromDB.Password != user.Password)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 1, 1) });
				#region Create token
				var signature = builder.Configuration["Jwt:TokenSignature"];
				var expireDate = builder.Configuration["Jwt:TokenExpireAsHour"];
				int.TryParse(expireDate, out var hourValue);
				var expireDateAsDate = DateTime.Now + TimeSpan.FromHours(hourValue);
#pragma warning disable CS8604 // Possible null reference argument.
				var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:EncryptionKey"]);
#pragma warning restore CS8604 // Possible null reference argument.
				var tokenId = Guid.NewGuid().ToString();
				var tokenDescriptor = new SecurityTokenDescriptor
				{
					Subject = new ClaimsIdentity(new[]
					{
		new Claim("tokenId", tokenId),
		new Claim("userId", $"{userFromDB.Id}"),
		new Claim("appId", $"{appid}"),
		new Claim("userName", user.Username),
		new Claim("emailOK", $"{userFromDB.IsEmailVerified}"),
			}),
					Expires = expireDateAsDate,
					Issuer = signature,
					Audience = signature,
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
				TokenModel tokenModel = new TokenModel()
				{
					TokenId = tokenId,
					UserId = userFromDB.Id,
					AppId = appid,
					Token = stringToken,
				};
				myDbContext.Tokens.Add(tokenModel);
				await myDbContext.SaveChangesAsync();
				#endregion
				#region Clear expired tokens
				var expiredTokens = await myDbContext.Tokens
				.Where(e => e.CreationDate >= DateTime.Now.AddDays(1)) // Süresi dolmuş tokenleri seç
				.ToListAsync(); // Asenkron olarak liste olarak al
				myDbContext.Tokens.RemoveRange(expiredTokens); // Seçilen tokenleri kaldır
				await myDbContext.SaveChangesAsync();
				#endregion
				return Results.Ok(new ApiModel() { Data = stringToken, Message = await LangHelper.ConvertToResponse(myDbContext, request, 1, 2) });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x3", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		});

		app.MapPost("/auth/register", [AllowAnonymous] async (HttpRequest request, MyDbContext myDbContext, LoginRegisterModel user) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[POST]/auth/register", 10, 10)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				#region Conflict and requirements check
				var userFromDB = await myDbContext.Users.FirstOrDefaultAsync(u => u.Username == user.Username);
				if (userFromDB is not null)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 5, 0) });
				if (user.Username.Length > 20 || user.Username.Length < 3)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 5, 1) });
				if (user.Password.Length > 30 || user.Password.Length < 5)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 5, 2) });
				#endregion
				#region Creating user
				UserModel model = new UserModel() { Username = user.Username, Password = user.Password, Email = user.Email };
				myDbContext.Users.Add(model);
				await myDbContext.SaveChangesAsync();
				#endregion
				#region Clear unverified users
				var expiredUsers = await myDbContext.Users
				.Where(e => e.CreationDate <= DateTime.Now.AddDays(7) && (e.IsEmailVerified == false && e.IsPhoneVerified == false)) // Süresi dolmuş tokenleri seç
				.ToListAsync(); // Asenkron olarak liste olarak al
				myDbContext.Users.RemoveRange(expiredUsers); // Seçilen tokenleri kaldır
				#endregion
				return Results.Ok(new ApiModel() { Message = await LangHelper.ConvertToResponse(myDbContext, request, 1, 3) });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x4", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		});

		app.MapPost("/system/sendcommand", async ([FromQuery] string AdminToken, HttpRequest request, MyDbContext myDbContext, DynamicObjectModel cmd) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[POST]/system/send", 10, 10)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				if (await AuthHelper.IsFullyAuthorized(myDbContext, AdminToken) is false)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 2, 0) });
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
				process.StandardInput.WriteLine(cmd.Object);
				process.StandardInput.Flush();
				process.StandardInput.Close();
				string output = process.StandardOutput.ReadToEnd();
				string error = process.StandardError.ReadToEnd();
				process.WaitForExit();

				if (string.IsNullOrEmpty(error))
				{
					await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "[POST]/system/sendcommand", output);
					return Results.Ok(new ApiModel() { Message = output });
				}
				else
				{
					await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "[POST]/system/sendcommand", error);
					return Results.Ok(new ApiModel() { Error = error });
				}
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x5", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		}).RequireAuthorization();

		app.MapPost("/user/update/password", async (HttpRequest request, MyDbContext myDbContext, DynamicObjectModel dynamicObjectModel) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[POST]/user/update/password", 10, 10)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				var user = await DbHelper.GetUserFromToken(request, myDbContext);
				if (user is null)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 4, 2) });
				if (user.IsEmailVerified is false)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 5, 3) });
				var newPassword = dynamicObjectModel.Object.ToString();
				if (newPassword == null || newPassword.Length > 30 || newPassword.Length < 5)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 5, 2) });
				user.Password = newPassword;
				myDbContext.Users.Update(user);
				await myDbContext.SaveChangesAsync();
				return Results.Ok(new ApiModel() { Message = await LangHelper.ConvertToResponse(myDbContext, request, 5, 4) });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x6", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		}).RequireAuthorization();

		#region MAIL
		app.MapPost("/mail/send", async ([FromQuery] string AdminToken, HttpRequest request, MyDbContext myDbContext, MailModel mailModel) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[POST]/mail/send", 1, 1)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				if (await AuthHelper.IsFullyAuthorized(myDbContext, AdminToken) is false)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 2, 0) });
				await MailHelper.SendMailAsync(myDbContext, builder, mailModel);
				return Results.Ok(new ApiModel() { Message = await LangHelper.ConvertToResponse(myDbContext, request, 3, 0) + mailModel.To });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x7", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		}).RequireAuthorization();

		app.MapPost("/mail/update", async (HttpRequest request, MyDbContext myDbContext, DynamicObjectModel uptadeObjectModel) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[POST]/user/update/email", 10, 1)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				var user = await DbHelper.GetUserFromToken(request, myDbContext);
				if (user is null)
					return Results.Json(new ApiModel() { Error = "Your token is invalid. Please login the your account again." });
				user.Email = uptadeObjectModel.Object.ToString();
				user.IsEmailVerified = false;
				myDbContext.Users.Update(user);
				await myDbContext.SaveChangesAsync();
				return Results.Ok(new ApiModel() { Message = "Your email is succesfully updated." });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x8", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		}).RequireAuthorization();

		app.MapPost("/mail/verify1", async (HttpRequest request, MyDbContext myDbContext) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[POST]/user/verify/email/step1", 600, 1)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				var user = await DbHelper.GetUserFromToken(request, myDbContext);
				if (user is null)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 4, 2) });
				if (user.Email is null)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 6, 0) });
				#region Clear expired verifications
				var expires = await myDbContext.Verifications
				.Where(e => e.CreationDate >= DateTime.Now.AddMinutes(1)) // Süresi dolmuş tokenleri seç
				.ToListAsync(); // Asenkron olarak liste olarak al
				myDbContext.Verifications.RemoveRange(expires); // Seçilen tokenleri kaldır
				await myDbContext.SaveChangesAsync();
				#endregion
				#region Clear old verifications
				var expires2 = await myDbContext.Verifications
				.Where(e => e.UserId == user.Id) // Süresi dolmuş tokenleri seç
				.ToListAsync(); // Asenkron olarak liste olarak al
				myDbContext.Verifications.RemoveRange(expires2); // Seçilen tokenleri kaldır
				await myDbContext.SaveChangesAsync();
				#endregion
				VerificationModel verificationModel = new VerificationModel() { UserId = user.Id };
				myDbContext.Verifications.Add(verificationModel);
				await myDbContext.SaveChangesAsync();
				string mailSubject = await LangHelper.ConvertToResponse(myDbContext, request, 6, 1);
				string mailBody = await LangHelper.ConvertToResponse(myDbContext, request, 6, 2) + "<h1>" + verificationModel.Code + "</h1>";
				await MailHelper.SendMailAsync(myDbContext, builder, new MailModel() { To = user.Email, Subject = mailSubject, Body = mailBody });
				return Results.Ok(new ApiModel() { Message = await LangHelper.ConvertToResponse(myDbContext, request, 6, 3) + user.Email });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x9", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		}).RequireAuthorization();

		app.MapPost("/mail/verify2", async (HttpRequest request, MyDbContext myDbContext, DynamicObjectModel dynamicObjectModel) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[POST]/user/verify/email/step2", 10, 1)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				var user = await DbHelper.GetUserFromToken(request, myDbContext);
				if (user is null)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 4, 2) });
				var verification = await myDbContext.Verifications.FirstOrDefaultAsync(a => a.Code == Convert.ToInt32(dynamicObjectModel.Object.ToString()));
				if (verification == null || (verification.UserId != user.Id))
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 6, 4) });
				user.IsEmailVerified = true;
				myDbContext.Users.Update(user);
				myDbContext.Verifications.Remove(verification);
				await myDbContext.SaveChangesAsync();
				return Results.Ok(new ApiModel() { Message = await LangHelper.ConvertToResponse(myDbContext, request, 6, 5) });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x10", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		}).RequireAuthorization();
		#endregion

		app.MapDelete("/db/tokens/{appid}", async ([FromQuery] string AdminToken, HttpRequest request, MyDbContext myDbContext, int appid) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[DEL]/db/Tokens/{appid}", 10, 1)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				if (await AuthHelper.IsFullyAuthorized(myDbContext, AdminToken) is false)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 2, 0) });
				var datas = await myDbContext.Tokens
				.Where(e => e.AppId == appid) // Süresi dolmuş tokenleri seç
				.ToListAsync(); // Asenkron olarak liste olarak al
				myDbContext.Tokens.RemoveRange(datas); // Seçilen tokenleri kaldır
				await myDbContext.SaveChangesAsync();
				return Results.Ok(new ApiModel() { Message = await LangHelper.ConvertToResponse(myDbContext, request, 4, 0) + (AppsFromIds)appid });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x11", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		}).RequireAuthorization();

		app.MapDelete("/db/tokens", async ([FromQuery] string AdminToken, HttpRequest request, MyDbContext myDbContext) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[DEL]/db/Tokens", 10, 1)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				if (await AuthHelper.IsFullyAuthorized(myDbContext, AdminToken) is false)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 2, 0) });
				myDbContext.Tokens.RemoveRange(myDbContext.Tokens);
				await myDbContext.SaveChangesAsync();
				return Results.Ok(new ApiModel() { Message = await LangHelper.ConvertToResponse(myDbContext, request, 4, 1) });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x12", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		}).RequireAuthorization();

		app.MapGet("/app/get", async ([FromQuery] string AppKey, HttpRequest request, MyDbContext myDbContext) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[GET]/app/get", 10, 1)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				if (string.IsNullOrEmpty(AppKey))
					return Results.Ok(new ApiModel() { Data = myDbContext.Apps.ToList() });
				else
				{
					var app = myDbContext.Apps.FirstOrDefault(f => f.Key.Equals(AppKey));
					if (app is null)
						throw new Exception(await LangHelper.ConvertToResponse(myDbContext, request, 8, 5));
					else
						return Results.Ok(new ApiModel() { Data = app });
				}
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x13", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		});

		app.MapPost("/app/insert", async ([FromQuery] string AdminToken, HttpRequest request, MyDbContext myDbContext, AppModel appModel) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[POST]/app/insert", 10, 1)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				if (await AuthHelper.IsFullyAuthorized(myDbContext, AdminToken) is false)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 2, 0) });
				var user = await DbHelper.GetUserFromToken(request, myDbContext);
				if (user is null)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 4, 2) });
				if (user.IsEmailVerified is false)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 5, 3) });
				try
				{
					if (string.IsNullOrEmpty(appModel.Key))
						throw new Exception(await LangHelper.ConvertToResponse(myDbContext, request, 8, 3));
					if (myDbContext.Apps.FirstOrDefault(f => f.Key.Equals(appModel.Key)) is not null)
						throw new Exception(await LangHelper.ConvertToResponse(myDbContext, request, 8, 4));
					await myDbContext.Apps.AddAsync(appModel);
					await myDbContext.SaveChangesAsync();
				}
				catch (Exception ex)
				{
					return Results.Json(new ApiModel() { Error = ex.Message });
				}
				return Results.Ok(new ApiModel() { Message = await LangHelper.ConvertToResponse(myDbContext, request, 8, 0) });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x14", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		}).RequireAuthorization();

		app.MapPost("/app/update", async ([FromQuery] string AdminToken, HttpRequest request, MyDbContext myDbContext, AppModel appModel) =>
		{
		try
		{
			if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[POST]/app/update", 10, 1)) != SpamLevel.Ok)
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
			if (await AuthHelper.IsFullyAuthorized(myDbContext, AdminToken) is false)
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 2, 0) });
			var user = await DbHelper.GetUserFromToken(request, myDbContext);
			if (user is null)
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 4, 2) });
			if (user.IsEmailVerified is false)
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 5, 3) });
			try
			{
				if (string.IsNullOrEmpty(appModel.Key))
					throw new Exception(await LangHelper.ConvertToResponse(myDbContext, request, 8, 3));
				if (myDbContext.Apps.AsNoTracking().FirstOrDefault(f => f.Key.Equals(appModel.Key)) is null || appModel.Id == 0)
					throw new Exception(await LangHelper.ConvertToResponse(myDbContext, request, 8, 5));
				myDbContext.Apps.Update(appModel);
				await myDbContext.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				return Results.Json(new ApiModel() { Error = ex.Message });
			}
			return Results.Ok(new ApiModel() { Message = await LangHelper.ConvertToResponse(myDbContext, request, 8, 1) });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x15", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		}).RequireAuthorization();

		app.MapDelete("/app/delete", async ([FromQuery] string AdminToken, HttpRequest request, MyDbContext myDbContext, [FromQuery] string AppKey) =>
		{
			try
			{
				if ((await ProtectionHelper.SpamCheckAsync(request, myDbContext, "[POST]/app/delete", 10, 1)) != SpamLevel.Ok)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 7, 0) });
				if (await AuthHelper.IsFullyAuthorized(myDbContext, AdminToken) is false)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 2, 0) });
				var user = await DbHelper.GetUserFromToken(request, myDbContext);
				if (user is null)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 4, 2) });
				if (user.IsEmailVerified is false)
					return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 5, 3) });

				var app = myDbContext.Apps.FirstOrDefault(f => f.Key.Equals(AppKey));
				if (app is null)
					throw new Exception(await LangHelper.ConvertToResponse(myDbContext, request, 8, 5));
				myDbContext.Apps.Remove(app);
				await myDbContext.SaveChangesAsync();
				return Results.Ok(new ApiModel() { Message = await LangHelper.ConvertToResponse(myDbContext, request, 8, 2) });
			}
			catch (Exception ex)
			{
				await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "0x16", ex.Message);
				return Results.Json(new ApiModel() { Error = await LangHelper.ConvertToResponse(myDbContext, request, 9, 0) });
			}
		}).RequireAuthorization();

		app.UseAuthentication();
		app.UseAuthorization();
		app.Run();
	}
}