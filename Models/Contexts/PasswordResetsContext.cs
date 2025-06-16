using System;
using Npgsql;
using Z_TRIP.Helpers;

namespace Z_TRIP.Models.Contexts
{
    public class PasswordResetContext
    {
        private readonly string _constr;

        public PasswordResetContext(string constr)
        {
            _constr = constr;
        }

        // Buat token reset password baru
        public int CreateToken(int userId, string token, DateTime expiresAt)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                INSERT INTO password_resets 
                    (user_id, token, expires_at, used, created_at)
                VALUES 
                    (@UserId, @Token, @ExpiresAt, false, CURRENT_TIMESTAMP)
                RETURNING id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Token", token);
            cmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);

            var result = cmd.ExecuteScalar();
            return result == null ? 0 : Convert.ToInt32(result);
        }

        // Validasi token
        public PasswordReset? ValidateToken(int userId, string token)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                SELECT id, user_id, token, expires_at, used, created_at
                FROM password_resets
                WHERE user_id = @UserId
                  AND token = @Token
                  AND expires_at > CURRENT_TIMESTAMP
                  AND used = false
                ORDER BY created_at DESC
                LIMIT 1";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Token", token);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new PasswordReset
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Token = reader.GetString(2),
                    ExpiresAt = reader.GetDateTime(3),
                    Used = reader.GetBoolean(4),
                    CreatedAt = reader.GetDateTime(5)
                };
            }

            return null;
        }

        // Ambil reset token berdasarkan token
        public PasswordReset? GetResetByToken(string token)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                SELECT id, user_id, token, expires_at, used, created_at
                FROM password_resets
                WHERE token = @Token
                LIMIT 1";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Token", token);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new PasswordReset
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Token = reader.GetString(2),
                    ExpiresAt = reader.GetDateTime(3),
                    Used = reader.GetBoolean(4),
                    CreatedAt = reader.GetDateTime(5)
                };
            }

            return null;
        }

        // Update token (setelah OTP diverifikasi)
        public bool UpdateToken(int resetId, string newToken)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE password_resets
                SET token = @NewToken
                WHERE id = @ResetId";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@ResetId", resetId);
            cmd.Parameters.AddWithValue("@NewToken", newToken);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Mark token sebagai used
        public bool MarkTokenAsUsed(int resetId)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE password_resets
                SET used = true
                WHERE id = @ResetId";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@ResetId", resetId);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Hapus token yang belum dipakai untuk user tertentu
        public bool DeleteUnusedTokensByUserId(int userId)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                DELETE FROM password_resets
                WHERE user_id = @UserId AND used = false";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@UserId", userId);

            return cmd.ExecuteNonQuery() >= 0; // Gunakan >= 0 untuk menangani kasus tidak ada token yang dihapus
        }
    }
}
