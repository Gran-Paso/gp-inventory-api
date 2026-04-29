using GPInventory.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GPInventory.Infrastructure.Services;

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ResendEmailService> _logger;

    private string ApiKey      => _config["Resend:ApiKey"]   ?? throw new InvalidOperationException("Resend:ApiKey not configured");
    private string FromAddress => _config["Resend:From"]     ?? "no-reply@granpasochile.cl";
    private string FromName    => _config["Resend:FromName"] ?? "Gran Paso";

    public ResendEmailService(HttpClient http, IConfiguration config, ILogger<ResendEmailService> logger)
    {
        _http   = http;
        _config = config;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="padding:40px 0;">
                <tr><td align="center">
                  <table width="480" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr><td style="background:#1a1a2e;padding:28px 32px;">
                      <p style="margin:0;color:#ffffff;font-size:20px;font-weight:700;letter-spacing:.5px;">Gran Paso</p>
                    </td></tr>
                    <tr><td style="padding:36px 32px;">
                      <h1 style="margin:0 0 12px;font-size:22px;color:#111827;">Restablecer contraseña</h1>
                      <p style="margin:0 0 24px;color:#6b7280;line-height:1.6;">Hola {toName}, recibimos una solicitud para restablecer la contraseña de tu cuenta. Haz clic en el botón a continuación — el enlace es válido por <strong>1 hora</strong>.</p>
                      <table cellpadding="0" cellspacing="0" style="margin-bottom:28px;">
                        <tr><td style="background:#4f46e5;border-radius:8px;">
                          <a href="{resetLink}" style="display:inline-block;padding:14px 28px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;">Restablecer contraseña</a>
                        </td></tr>
                      </table>
                      <p style="margin:0 0 8px;color:#9ca3af;font-size:13px;">Si no puedes hacer clic en el botón, copia y pega este enlace:</p>
                      <p style="margin:0 0 24px;word-break:break-all;font-size:12px;color:#4f46e5;">{resetLink}</p>
                      <hr style="border:none;border-top:1px solid #f0f0f0;margin:0 0 20px;">
                      <p style="margin:0;color:#9ca3af;font-size:12px;">Si no solicitaste este cambio, ignora este correo. Tu contraseña permanecerá igual.</p>
                    </td></tr>
                    <tr><td style="background:#f9fafb;padding:16px 32px;text-align:center;">
                      <p style="margin:0;color:#9ca3af;font-size:12px;">© {DateTime.UtcNow.Year} Gran Paso · granpasochile.cl</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        await SendAsync(toEmail, "Restablecer tu contraseña — Gran Paso", html);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string toName)
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="padding:40px 0;">
                <tr><td align="center">
                  <table width="480" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr><td style="background:#1a1a2e;padding:28px 32px;">
                      <p style="margin:0;color:#ffffff;font-size:20px;font-weight:700;letter-spacing:.5px;">Gran Paso</p>
                    </td></tr>
                    <tr><td style="padding:36px 32px;">
                      <h1 style="margin:0 0 12px;font-size:22px;color:#111827;">¡Bienvenido, {toName}!</h1>
                      <p style="margin:0 0 24px;color:#6b7280;line-height:1.6;">Tu cuenta en Gran Paso ha sido creada exitosamente. Ahora puedes acceder a todas las herramientas del ecosistema según los permisos asignados a tu negocio.</p>
                      <table cellpadding="0" cellspacing="0" style="margin-bottom:28px;">
                        <tr><td style="background:#4f46e5;border-radius:8px;">
                          <a href="https://auth.granpasochile.cl" style="display:inline-block;padding:14px 28px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;">Ingresar a Gran Paso</a>
                        </td></tr>
                      </table>
                      <hr style="border:none;border-top:1px solid #f0f0f0;margin:0 0 20px;">
                      <p style="margin:0;color:#9ca3af;font-size:12px;">Si tienes alguna duda, responde a este correo y te ayudamos.</p>
                    </td></tr>
                    <tr><td style="background:#f9fafb;padding:16px 32px;text-align:center;">
                      <p style="margin:0;color:#9ca3af;font-size:12px;">© {DateTime.UtcNow.Year} Gran Paso · granpasochile.cl</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        await SendAsync(toEmail, "¡Bienvenido a Gran Paso!", html);
    }

    public async Task SendEmailVerificationAsync(string toEmail, string toName, string verifyLink)
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="padding:40px 0;">
                <tr><td align="center">
                  <table width="480" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr><td style="background:#1a1a2e;padding:28px 32px;">
                      <p style="margin:0;color:#ffffff;font-size:20px;font-weight:700;letter-spacing:.5px;">Gran Paso</p>
                    </td></tr>
                    <tr><td style="padding:36px 32px;">
                      <h1 style="margin:0 0 12px;font-size:22px;color:#111827;">Confirma tu correo electrónico</h1>
                      <p style="margin:0 0 24px;color:#6b7280;line-height:1.6;">Hola {toName}, haz clic en el botón a continuación para verificar tu dirección de correo. El enlace es válido por <strong>24 horas</strong>.</p>
                      <table cellpadding="0" cellspacing="0" style="margin-bottom:28px;">
                        <tr><td style="background:#4f46e5;border-radius:8px;">
                          <a href="{verifyLink}" style="display:inline-block;padding:14px 28px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;">Verificar correo</a>
                        </td></tr>
                      </table>
                      <p style="margin:0 0 8px;color:#9ca3af;font-size:13px;">Si no puedes hacer clic en el botón, copia y pega este enlace:</p>
                      <p style="margin:0 0 24px;word-break:break-all;font-size:12px;color:#4f46e5;">{verifyLink}</p>
                      <hr style="border:none;border-top:1px solid #f0f0f0;margin:0 0 20px;">
                      <p style="margin:0;color:#9ca3af;font-size:12px;">Si no creaste una cuenta en Gran Paso, ignora este correo.</p>
                    </td></tr>
                    <tr><td style="background:#f9fafb;padding:16px 32px;text-align:center;">
                      <p style="margin:0;color:#9ca3af;font-size:12px;">© {DateTime.UtcNow.Year} Gran Paso · granpasochile.cl</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        await SendAsync(toEmail, "Verifica tu correo electrónico — Gran Paso", html);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task SendAsync(string toEmail, string subject, string html)
    {
        var payload = new
        {
            from    = $"{FromName} <{FromAddress}>",
            to      = new[] { toEmail },
            subject,
            html,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Resend API error {Status}: {Body}", response.StatusCode, body);
            // No lanzar excepción — el flujo de negocio no debe fallar si el email falla
        }
        else
        {
            _logger.LogInformation("Email enviado a {Email} — asunto: {Subject}", toEmail, subject);
        }
    }
}
