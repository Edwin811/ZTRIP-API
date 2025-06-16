using System.Text.Json.Serialization;

namespace Z_TRIP.Models
{
    public class Users
    {
        public int Id { get; set; }

        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        
        // Ubah dari string? menjadi byte[]? untuk menyimpan binary data gambar profil
        public byte[]? Profile { get; set; }

        // Binary data untuk KTP/SIM
        public byte[]? KtpImage { get; set; }
        public byte[]? SimImage { get; set; }

        public string Password { get; set; } = string.Empty;
        public bool Role { get; set; } = false;
        public bool IsVerified { get; set; } = false;
        
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
