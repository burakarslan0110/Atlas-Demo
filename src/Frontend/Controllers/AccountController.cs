using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Frontend.Controllers;

public class AccountController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IHttpClientFactory httpClientFactory, ILogger<AccountController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var client = _httpClientFactory.CreateClient("ApiGateway");

        var registerData = new
        {
            email = model.Email,
            password = model.Password,
            firstName = model.FirstName,
            lastName = model.LastName
        };

        var content = new StringContent(JsonSerializer.Serialize(registerData), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/auth/register", content);

        if (response.IsSuccessStatusCode)
        {
            TempData["Success"] = "Kayıt başarılı! Giriş yapabilirsiniz.";
            return RedirectToAction(nameof(Login));
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        ModelState.AddModelError(string.Empty, "Kayıt başarısız: " + errorContent);
        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        var client = _httpClientFactory.CreateClient("ApiGateway");

        var loginData = new
        {
            email = model.Email,
            password = model.Password
        };

        var content = new StringContent(JsonSerializer.Serialize(loginData), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/auth/login", content);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (authResponse != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, authResponse.User.Id.ToString()),
                    new Claim(ClaimTypes.Email, authResponse.User.Email),
                    new Claim(ClaimTypes.GivenName, authResponse.User.FirstName),
                    new Claim(ClaimTypes.Surname, authResponse.User.LastName),
                    new Claim(ClaimTypes.Role, authResponse.User.Role),
                    new Claim("jwt_token", authResponse.Token)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = authResponse.ExpiresAt
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                HttpContext.Session.SetString("jwt_token", authResponse.Token);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }
        }

        ModelState.AddModelError(string.Empty, "Email veya şifre hatalı.");
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var token = HttpContext.Session.GetString("jwt_token");

        if (!string.IsNullOrEmpty(token))
        {
            var client = _httpClientFactory.CreateClient("ApiGateway");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            await client.PostAsync("/api/auth/logout", null);
        }

        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var client = _httpClientFactory.CreateClient("ApiGateway");

        var forgotPasswordData = new { email = model.Email };
        var content = new StringContent(JsonSerializer.Serialize(forgotPasswordData), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/forgot-password", content);

        if (response.IsSuccessStatusCode)
        {
            TempData["Success"] = "Eğer bu e-posta adresi ile kayıtlı bir hesap varsa, şifre sıfırlama bağlantısı gönderildi. Lütfen e-postanızı kontrol edin.";
            return View();
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        _logger.LogWarning("Forgot password failed for {Email}: {Error}", model.Email, errorContent);

        TempData["Error"] = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? token)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        if (string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "Geçersiz şifre sıfırlama bağlantısı.";
            return RedirectToAction(nameof(ForgotPassword));
        }


        var client = _httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync($"/api/auth/validate-reset-token/{token}");

        var model = new ResetPasswordViewModel { Token = token };
        ViewBag.TokenValid = response.IsSuccessStatusCode;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.TokenValid = true;
            return View(model);
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError(string.Empty, "Şifreler eşleşmiyor.");
            ViewBag.TokenValid = true;
            return View(model);
        }

        var client = _httpClientFactory.CreateClient("ApiGateway");

        var resetPasswordData = new
        {
            token = model.Token,
            newPassword = model.NewPassword
        };
        var content = new StringContent(JsonSerializer.Serialize(resetPasswordData), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/reset-password", content);

        if (response.IsSuccessStatusCode)
        {
            TempData["Success"] = "Şifreniz başarıyla sıfırlandı! Artık yeni şifrenizle giriş yapabilirsiniz.";
            return RedirectToAction(nameof(Login));
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        _logger.LogWarning("Reset password failed: {Error}", errorContent);

        TempData["Error"] = "Şifre sıfırlama başarısız. Bağlantı geçersiz veya süresi dolmuş olabilir.";
        ViewBag.TokenValid = false;
        return View(model);
    }
}


public class RegisterViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class LoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class ForgotPasswordViewModel
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
