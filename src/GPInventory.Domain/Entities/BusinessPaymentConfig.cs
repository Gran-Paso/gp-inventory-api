using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPInventory.Domain.Entities;

/// <summary>
/// Configuración de medios de pago por negocio.
/// Los valores sensibles (access tokens) se almacenan encriptados con AES-256.
/// La clave de encriptación vive en MercadoPago:EncryptionKey del servidor, nunca en la DB.
/// </summary>
[Table("business_payment_config")]
public class BusinessPaymentConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("business_id")]
    public int BusinessId { get; set; }

    // ── MercadoPago ──────────────────────────────────────────────────────────

    /// <summary>Access Token de producción encriptado con AES-256.</summary>
    [Column("mp_access_token_encrypted")]
    [StringLength(512)]
    public string? MpAccessTokenEncrypted { get; set; }

    /// <summary>Clave pública (no sensible, usada en el front si fuera necesario).</summary>
    [Column("mp_public_key")]
    [StringLength(100)]
    public string? MpPublicKey { get; set; }

    /// <summary>ID del dispositivo Point Smart registrado en la cuenta MP del negocio.</summary>
    [Column("mp_point_device_id")]
    [StringLength(100)]
    public string? MpPointDeviceId { get; set; }

    /// <summary>Secret para validar firmas de webhooks MP, encriptado.</summary>
    [Column("mp_webhook_secret_encrypted")]
    [StringLength(512)]
    public string? MpWebhookSecretEncrypted { get; set; }

    /// <summary>Indica si la integración MP está activa para este negocio.</summary>
    [Column("mp_enabled")]
    public bool MpEnabled { get; set; } = false;

    // ── Auditoría ────────────────────────────────────────────────────────────

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Navegación ───────────────────────────────────────────────────────────

    public Business Business { get; set; } = null!;
}
